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
public class FeatureManager : MonoBehaviour
{
    public static FeatureManager m_Instance = null;

    [SerializeField] private List<SOFeature> m_listAllFeatureSO = new List<SOFeature>();

    private SOFeature[] m_arrFeatureByID;
    private int[] m_arrAcquiredLevel;

    // RequestFeatureChoices 내부에서 재사용하는 버퍼 (호출마다 알록 방지)
    private List<SOFeature> m_listPoolBuffer = new List<SOFeature>();

    public event Action<List<SOFeature>> OnFeatureChoicesReady;
    public event Action<SOFeature, int> OnFeatureAcquired;

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(this);

        m_Instance = this;
        DontDestroyOnLoad(this);

        BuildFeatureTable();

        UnityEngine.Random.InitState((int)System.DateTime.Now.Ticks);
    }

    private void BuildFeatureTable()
    {
        int iCount = (int)eFeatureID.End;
        m_arrFeatureByID = new SOFeature[iCount];
        m_arrAcquiredLevel = new int[iCount]; //해당 기능의 레벨이 현재 몇인지 체크하는 배열

        for (int i = 0; i < m_listAllFeatureSO.Count; ++i)
        {
            SOFeature refFeature = m_listAllFeatureSO[i];
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
    public SOFeature RequestFeatureChoice()
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
        
        SOFeature refPicked = PickWeightedRandom(m_listPoolBuffer);
        return refPicked;

    }
    
    private SOFeature PickWeightedRandom(List<SOFeature> _listPool)
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
                return _listPool[i];
        }

        return _listPool[_listPool.Count - 1];
    }

    public void SelectFeature(SOFeature _refFeature, Player _refPlayer)
    {
        if (_refFeature == null || _refPlayer == null)
            return;

        int iIndex = (int)_refFeature.ID;
        m_arrAcquiredLevel[iIndex]++;

        _refFeature.Apply(_refPlayer, m_arrAcquiredLevel[iIndex]);

        OnFeatureAcquired?.Invoke(_refFeature, m_arrAcquiredLevel[iIndex]);
    }
}
