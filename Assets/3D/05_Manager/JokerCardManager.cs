using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.Mesh;

/*///////////////////////////////////////////
                JokerCardManager
기능 : 조커카드 도박 진행 상태(연속 성공 횟수, 아직 미확정인 보류 카드 목록)를 관리하는 매니저
      선택된 카드는 즉시 Player에 적용하지 않고 보류시켰다가, 현금화(CashOut) 시점에
      FeatureManager.SelectFeature로 일괄 적용한다.
      실패 시에는 보류 목록을 비우기만 하면 되므로(=몰수) SOFeature에 별도 Revert 로직이 필요 없다.
 *///////////////////////////////////////////

public class JokerCardManager : MonoBehaviour
{
    public static JokerCardManager m_Instance = null;

    [SerializeField] private SOJokerCard m_SOJokerCard = null;

    private int m_iLevel = 0;
    public int Level => m_iLevel;

    // ApplySuccess()에서 재사용하는 등급별 배율 버퍼 (호출마다 알록 방지, 인덱스 = eFeatureTier)
    private float[] m_arrTierMultiplierBuffer = new float[(int)eFeatureTier.End];

    private List<SOFeature> m_listPendingFeature = new List<SOFeature>();
    public IReadOnlyList<SOFeature> PendingFeature => m_listPendingFeature;

    // 현재 내가 적용한 기능들
    //private List<SOFeature> m_listApplyFeature = new List<SOFeature>();

    [SerializeField] private Container m_refPickContainer;   //내가 고른 기능
    [SerializeField] private Container m_refSelectContainer; //내가 선택할 수 있는 기능
    [SerializeField] private Container m_refFeatContainer;   //내가 가지고 있는 기능

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
    }

    private void Start()
    {
        m_refPickContainer.Init();
        m_refSelectContainer.Init();
        m_refFeatContainer.Init();

        //만약 데이터를 골랐다면 다른 컨테이너에서 선택할 수 있게
        m_refSelectContainer.OnSelectEvt += AddData;

        m_refPickContainer.OnSelectEvt += DeleteData;

        //Container/SlotView는 SOData 범용이라 등급을 모름 - 조커 후보 슬롯에 뜨는 등급 색상은
        //SOFeature/Tier를 이미 알고 있는 이쪽(JokerCardManager)에서 판단해서 밀어준다
        m_refFeatContainer.OnSlotBind += ApplyTierColor;
        m_refSelectContainer.OnSlotBind += ApplyTierColor;
        m_refPickContainer.OnSlotBind += ApplyTierColor;
    }


    // 도박 판정만 수행 
    public bool RollGamble(SOJokerCard _SOJokerCard)
    {
        if (_SOJokerCard == null)
            return false;

        m_SOJokerCard = _SOJokerCard;
        return Random.value < m_SOJokerCard.GetSuccessValue(m_iLevel);
    }

    // 도박 성공 결과 반영: 이번 회차의 후보 목록을 컨테이너에 표시
    public void ApplySuccess()
    {
        m_refPickContainer.gameObject.SetActive(true);
        m_refSelectContainer.gameObject.SetActive(true);

        m_iLevel++;

        //내 조커 레벨에 맞게 나오는 티어 가중치 변경
        m_SOJokerCard.UpdateTierWeight(m_arrTierMultiplierBuffer, m_iLevel);

        //이전 회차(미확정) 후보가 남아있으면 새 후보와 섞여 누적 표시되므로, 채우기 전에 비우고 이번 회차 후보 수로 리셋
        //(Resize는 이전 카운트와 같으면 아무 동작도 하지 않아 남은 데이터를 지우지 못하므로 ClearData를 먼저 호출)
        int iCandidateCount = m_SOJokerCard.GetCandidateCount(m_iLevel);
        m_refSelectContainer.ClearData();
        m_refSelectContainer.Resize(iCandidateCount, eDataType.Features);

        m_listPendingFeature = FeatureManager.m_Instance.RequestFeature(iCandidateCount, m_arrTierMultiplierBuffer);
        for (int i = 0; i < m_listPendingFeature.Count; ++i)
            m_refSelectContainer.AddData(m_listPendingFeature[i], 1);

        //내가 고를 수 있는 사이즈만큼 컨테이너 사이즈 줄이기
        int iPickCount = GetCurrentPickCount();
        m_refPickContainer.Resize(iPickCount, eDataType.Features);
    }

    // 도박 실패 결과 반영: 보류 목록 몰수
    public void ApplyFail() => ClearFeature();

    public int GetCurrentPickCount() => m_SOJokerCard.GetPickCount(m_iLevel);

    public float GetSuccessValue() => m_SOJokerCard.GetSuccessValue(m_iLevel);

    private void AddData(SOData _SOData)
    {
        var SOFind = m_refPickContainer.FindData(_SOData);
        if (SOFind != null)
            return;

        m_refPickContainer.AddData(_SOData, 1);
    }

    private void DeleteData(SOData _SOData)
    {
        m_refPickContainer.DeleteData(_SOData);
    }

    // 실제 SOFeature 카드가 뜬 슬롯에만 등급색을 입힌다 - 빈 슬롯 리셋은 SlotView가 자체적으로 처리
    private void ApplyTierColor(SOData _SOData, SlotView _refSlot)
    {
        if (_SOData is not SOFeature refFeature)
            return;

        _refSlot.SetTierColor(FeatureTierUI.GetColor(refFeature.Tier));
    }

    //유니티 이벤트로 직렬화해서 연결 (Container UI의 PickButton)
    public void PickData()
    {
        CategoryData refData = m_refPickContainer.GetCategoryData(eDataType.Features);
        if (refData.IsFull == false)
            return;

        var listData = refData.ListData;
        Player refTarget = Player.CurrentPlayer;

        for (int i = 0; i< listData.Count; ++i)
        {
            SOFeature SOTarget = listData[i] as SOFeature;
            FeatureManager.m_Instance.SelectFeature(SOTarget, refTarget);
        }
        //m_listApplyFeature.AddRange(listData);
        

        m_refSelectContainer.ClearData();
        m_refPickContainer.ClearData();

        m_refSelectContainer.gameObject.SetActive(false);
        m_refPickContainer.gameObject.SetActive(false);

        Time.timeScale = 1.0f;
    }


    private void ClearFeature()
    {
        int iLostCount = m_SOJokerCard.GetLostCount(m_iLevel);
        Player refTarget = Player.CurrentPlayer;
        FeatureManager.m_Instance.CancelFeature(refTarget, iLostCount);

        m_listPendingFeature.Clear();
        m_iLevel = 0;
        Time.timeScale = 1.0f;

    }
}
