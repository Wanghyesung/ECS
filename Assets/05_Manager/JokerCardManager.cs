using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

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

    private SOJokerCard m_SOJokerCard = null;

    private int m_iLevel = 0;
    public int Level => m_iLevel;

    private List<SOFeature> m_listPendingFeature = new List<SOFeature>();
    public IReadOnlyList<SOFeature> PendingFeature => m_listPendingFeature;

    // 현재 내가 적용한 기능들
    //private List<SOFeature> m_listApplyFeature = new List<SOFeature>();

    [SerializeField] private Container m_refPickContainer;   //내가 고를 기능
    [SerializeField] private Container m_refSelectContainer; //내가 선택한 기능

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(this);

        m_Instance = this;
    }

    private void Start()
    {
        //만약 데이터를 골랐다면 다른 컨테이너에서 선택할 수 있게
        m_refSelectContainer.OnSelectEvt += AddData;

        m_refPickContainer.OnSelectEvt += DeleteData;
    }


    // 도박 1회 시도. 성공하면 이번 회차의 후보 목록을 반환하고, 실패하면 보류 목록을 몰수(초기화)한다.
    public void TryGamble(SOJokerCard _SOJokerCard)
    {
        if (_SOJokerCard == null)
            return;

        m_SOJokerCard = _SOJokerCard;
        bool bSuccess = Random.value < m_SOJokerCard.GetSuccessChance(m_iLevel);
        if (bSuccess == true)
        {
            m_refPickContainer.gameObject.SetActive(true);
            m_refSelectContainer.gameObject.SetActive(true);

            m_iLevel++;
            m_listPendingFeature = FeatureManager.m_Instance.RequestFeature(m_SOJokerCard.GetCandidateCount(m_iLevel));
            for (int i = 0; i < m_listPendingFeature.Count; ++i)
                m_refSelectContainer.AddData(m_listPendingFeature[i], 1);


            //내가 고를 수 있는 사이즈만큼 컨테이너 사이즈 줄이기
            int iPickCount = GetCurrentPickCount();
            m_refPickContainer.Resize(iPickCount);

        }
        else
            ClearFeature();
       
    }

    public int GetCurrentPickCount() => m_SOJokerCard.GetPickCount(m_iLevel);


    private void AddData(SOData _SOData)
    {
        var SOFind = m_refPickContainer.FindData(_SOData);
        if (SOFind != null)
            return;

        m_refPickContainer.AddData(_SOData, 1);
    }

    private void DeleteData(SOData _SOData)
    {
        m_refPickContainer.DeleteData(_SOData, 0);
    }

    //유니티 이벤트로 직렬화해서 연결 (Container UI의 CloseButton)
    public void PickData()
    {
        CategoryData refData = m_refPickContainer.GetCategoryData(0);
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
