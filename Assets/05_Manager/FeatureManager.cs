using System;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                FeatureManager
기능 : 레벨업 등 특정 상황에서 플레이어에게 제시할 랜덤 기능(FeatureSO) 후보를 뽑고,
      선택된 기능을 적용/추적하는 매니저.
      런타임 획득 상태(레벨)는 SO가 아닌 이 매니저의 배열이 전담 (SO 데이터 오염 방지)
      기능 목록은 고정이고 런타임 추가/삭제가 없어서 eFeatureID를 인덱스로 쓰는
      배열 접근 방식을 사용 (Dictionary의 해시/버킷 탐색 비용 회피)
 *///////////////////////////////////////////
public class FeatureManager : MonoBehaviour
{
    public static FeatureManager m_Instance = null;

    [SerializeField] private List<FeatureSO> m_listAllFeatureSO = new List<FeatureSO>();

    private FeatureSO[] m_arrFeatureByID;
    private int[] m_arrAcquiredLevel;

    // RequestFeatureChoices 내부에서 재사용하는 버퍼 (호출마다 알록 방지)
    private List<FeatureSO> m_listPoolBuffer = new List<FeatureSO>();

    public event Action<List<FeatureSO>> OnFeatureChoicesReady;
    public event Action<FeatureSO, int> OnFeatureAcquired;

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(this);

        m_Instance = this;
        DontDestroyOnLoad(this);

        BuildFeatureTable();
    }

    private void BuildFeatureTable()
    {
        int iCount = (int)eFeatureID.End;
        m_arrFeatureByID = new FeatureSO[iCount];
        m_arrAcquiredLevel = new int[iCount];

        for (int i = 0; i < m_listAllFeatureSO.Count; ++i)
        {
            FeatureSO refFeature = m_listAllFeatureSO[i];
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
    public List<FeatureSO> RequestFeatureChoices(int _iCount)
    {
        m_listPoolBuffer.Clear();

        for (int i = 0; i < m_arrFeatureByID.Length; ++i)
        {
            FeatureSO refFeature = m_arrFeatureByID[i];
            if (refFeature == null)
                continue;

            if (refFeature.AcquireType == eAcquireType.OneTime && m_arrAcquiredLevel[i] > 0)
                continue;

            m_listPoolBuffer.Add(refFeature);
        }

        int iPickCount = Mathf.Min(_iCount, m_listPoolBuffer.Count);

        // 결과 리스트는 호출부(UI)로 넘겨줘야 해서 매번 새로 생성.
        // 레벨업처럼 드물게 발생하는 이벤트라 알록이 성능에 영향 없음 (매 프레임 호출 X)
        List<FeatureSO> listResult = new List<FeatureSO>(iPickCount);
        for (int i = 0; i < iPickCount; ++i)
        {
            FeatureSO refPicked = PickWeightedRandom(m_listPoolBuffer);
            listResult.Add(refPicked);
            m_listPoolBuffer.Remove(refPicked);
        }

        OnFeatureChoicesReady?.Invoke(listResult);
        return listResult;
    }

    private FeatureSO PickWeightedRandom(List<FeatureSO> _listPool)
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

    public void SelectFeature(FeatureSO _refFeature, Player _refPlayer)
    {
        if (_refFeature == null || _refPlayer == null)
            return;

        int iIndex = (int)_refFeature.ID;
        m_arrAcquiredLevel[iIndex]++;

        _refFeature.Apply(_refPlayer, m_arrAcquiredLevel[iIndex]);

        OnFeatureAcquired?.Invoke(_refFeature, m_arrAcquiredLevel[iIndex]);
    }
}
