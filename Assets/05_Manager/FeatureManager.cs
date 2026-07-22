using System;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                FeatureManager
기능 : 레벨업 등 특정 상황에서 플레이어에게 제시할 랜덤 기능(FeatureSO) 후보를 뽑고,
      선택된 기능을 적용/추적하는 매니저
      런타임 획득 상태(레벨)는 SO가 아닌 이 매니저의 배열이 전담 (SO 데이터 오염 방지)
      기능 목록은 고정이고 런타임 추가/삭제가 없어서 eFeatureID를 인덱스로 쓰는
      배열 접근 방식을 사용 (Dictionary의 해시/버킷 탐색 비용 회피)
 *///////////////////////////////////////////
public class FeatureManager : MonoBehaviour, ICountable
{
    public static FeatureManager m_Instance = null;

    [SerializeField] private List<SOFeature> m_listFeatureSO = new List<SOFeature>();

    private SOFeature[] m_arrFeatureByID;
    private int[] m_arrAcquiredLevel;

    // RequestFeature 내부에서 재사용하는 버퍼 (호출마다 알록 방지)
    private List<SOFeature> m_listPoolBuffer = new List<SOFeature>();
    private List<SOFeature> m_listResultBuffer = new List<SOFeature>();
    private List<SOFeature> m_listApplyBuffer = new List<SOFeature>();

    public event Action<SOFeature, int> OnFeatureSelect;
    public event Action<SOFeature, int> OnFeatureDelete;


    [SerializeField] private List<SOFeature> m_listTemFeautre; //임시로 넣은 플레이어 기능


    private void Awake()
    {
        if (m_Instance != null)
            Destroy(this);

        m_Instance = this;
        DontDestroyOnLoad(this);

        Build();

        UnityEngine.Random.InitState((int)System.DateTime.Now.Ticks);
    }

    private void Start()
    {
        Player refTarget = Player.CurrentPlayer;
        for (int i = 0; i < m_listTemFeautre.Count; ++i)
        {
            SOFeature SOTarget = m_listTemFeautre[i];
            int iIndex = (int)SOTarget.ID;
            m_arrAcquiredLevel[iIndex]++;

            SOTarget.Apply(refTarget, m_arrAcquiredLevel[iIndex]);
        }
    }
    private void Build()
    {
        int iCount = (int)eFeatureID.End;
        m_arrFeatureByID = new SOFeature[iCount];
        m_arrAcquiredLevel = new int[iCount]; //해당 기능의 레벨이 현재 몇인지 체크하는 배열

        for (int i = 0; i < m_listFeatureSO.Count; ++i)
        {
            SOFeature refFeature = m_listFeatureSO[i];
            int iIndex = (int)refFeature.ID;

            if (iIndex <= (int)eFeatureID.None || iIndex >= iCount)
            {
                Debug.Log("FeatureSO ID 미설정 : " + refFeature.name);
                continue;
            }

            if (m_arrFeatureByID[iIndex] != null)
            {
                Debug.Log("FeatureSO ID 중복 : " + refFeature.name);
                continue;
            }

            m_arrFeatureByID[iIndex] = refFeature;
        }
    }

    // _iCount : 이번에 보여줄 후보 개수 (상황에 따라 가변으로 호출부에서 결정)
    // 후보가 _iCount보다 적으면 있는 만큼만 반환 (호출부에서 남는 카드 슬롯 처리)
    public List<SOFeature> RequestFeature(int _iCount)
    {
        m_listPoolBuffer.Clear();

        for (int i = 0; i < m_arrFeatureByID.Length; ++i)
        {
            SOFeature refFeature = m_arrFeatureByID[i];
            if (refFeature == null)
                continue;

            //한번만 나오는 카드고 이미 흭득했다면 무시
            if (refFeature.AcquireType == eAcquireType.OneTime && m_arrAcquiredLevel[i] > 0)
                continue;

            m_listPoolBuffer.Add(refFeature);
        }

        m_listResultBuffer.Clear();
        int iPickCount = Mathf.Min(_iCount, m_listPoolBuffer.Count);

        for (int i = 0; i < iPickCount; ++i)
        {
            int iPickedIdx = PickWeightedIndex(m_listPoolBuffer);
            m_listResultBuffer.Add(m_listPoolBuffer[iPickedIdx]);

            // 순서 상관없으니 마지막 원소와 교체 후 마지막을 제거 (앞당김 셔프트 없이 O(1))
            int iLastIdx = m_listPoolBuffer.Count - 1;
            m_listPoolBuffer[iPickedIdx] = m_listPoolBuffer[iLastIdx];
            m_listPoolBuffer.RemoveAt(iLastIdx);
        }

        return m_listResultBuffer;
    }

    private int PickWeightedIndex(List<SOFeature> _listPool)
    {
        int iTotalWeight = 0;
        for (int i = 0; i < _listPool.Count; ++i)
            iTotalWeight += _listPool[i].Weight;

        int iRandomValue = UnityEngine.Random.Range(0, iTotalWeight);
        int iAccum = 0;

        for (int i = 0; i < _listPool.Count; ++i)
        {
            iAccum += _listPool[i].Weight;
            if (iRandomValue < iAccum)
                return i;
        }

        return _listPool.Count - 1;
    }

    public int GetLevel(eFeatureID _eID)
    {
        int iIndex = (int)_eID;
        if (iIndex <= (int)eFeatureID.None || iIndex >= m_arrAcquiredLevel.Length)
            return 0;

        return m_arrAcquiredLevel[iIndex];
    }

    //ICountable 구현: Container가 카테고리별 카운트 출처로 참조
    public int GetCount(SOData _SOData)
    {
        if (_SOData is not SOFeature SOFeature)
            return 0;

        return GetLevel(SOFeature.ID);
    }

    public void SelectFeature(SOFeature _SOFeature, Player _refPlayer)
    {
        if (_SOFeature == null || _refPlayer == null)
            return;

        int iIndex = (int)_SOFeature.ID;
        m_arrAcquiredLevel[iIndex]++;

        _SOFeature.Apply(_refPlayer, m_arrAcquiredLevel[iIndex]);
        OnFeatureSelect?.Invoke(_SOFeature, m_arrAcquiredLevel[iIndex]);

        m_listApplyBuffer.Add(_SOFeature);
    }

    public void CancelFeature(Player _refPlayer, int _iLostCount)
    {
        for(int i = 0; i<_iLostCount; ++i)
        {
            int iBufferSize = m_listApplyBuffer.Count;
            if (iBufferSize <= 0)
                return;

            int iRandomIdx = UnityEngine.Random.Range(0, iBufferSize);

            SOFeature SOTarget = m_listApplyBuffer[iRandomIdx];
            m_listApplyBuffer.RemoveAt(iRandomIdx);

            int iIndex = (int)SOTarget.ID;
            SOTarget.Cancel(_refPlayer, m_arrAcquiredLevel[iIndex]);
            m_arrAcquiredLevel[iIndex] = 0;
        }
    }
}
