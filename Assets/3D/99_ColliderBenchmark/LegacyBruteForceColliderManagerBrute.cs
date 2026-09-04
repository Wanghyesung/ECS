using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

/*///////////////////////////////////////////
        LegacyBruteForceColliderManagerBrute
목적 : 커밋 421d1b0(그리드 도입 직전) 시점 ColliderManager의 판정 알고리즘을 그대로
       포팅한 벤치마크 전용 매니저 - 레이어 0~31 리스트를 이중 순회(N×M)해서 전부
       대조하는 순수 브루트포스. 그리드/Job/Burst는 전혀 없다.

       BruteForceTestScene 전용 독립 스냅샷이다 - LegacyBruteForceColliderManager(V1)는
       이후 8edea44 실측(GridTestScene0)을 위해 Box 그리드가 얹혀 더 이상 순수
       브루트포스가 아니게 됐으므로, "진짜 처음" 수치를 계속 재현하려면 이 파일이
       따로 있어야 한다. 판정 로직(Dictionary 기반 Enter/Stay/Exit, 레이어 매트릭스,
       도형별 디스패치)은 원본과 100% 동일하고, RaycastMask/FindAllInRadius/
       FindNearest 같은 조회 API만 뺐다.
 *///////////////////////////////////////////
[DefaultExecutionOrder(1000)]
public sealed class LegacyBruteForceColliderManagerBrute : MonoBehaviour
{
    public static LegacyBruteForceColliderManagerBrute Instance { get; private set; }

    private readonly LayerMask[] m_arrLayerCollisionMatrix = new LayerMask[32];

    private List<LegacyBruteForceColliderBrute>[] m_arrCollider;
    private List<LegacyBruteForceColliderBrute> m_listPendingDelete;
    private List<int> m_listIndexInLayerList;
    private List<HashSet<int>> m_listOther;
    private Dictionary<long, tColliderPair> m_hashPairInfo;

    private static readonly ProfilerMarker s_tMarkerLateUpdate = new ProfilerMarker("LegacyBruteForceBrute.LateUpdate");
    private static readonly ProfilerMarker s_tMarkerRefreshCenter = new ProfilerMarker("LegacyBruteForceBrute.PreLoadCenter");
    private static readonly ProfilerMarker s_tMarkerSameLayer = new ProfilerMarker("LegacyBruteForceBrute.CheckSameLayer");
    private static readonly ProfilerMarker s_tMarkerCrossLayer = new ProfilerMarker("LegacyBruteForceBrute.CheckCrossLayer");
    private static readonly ProfilerMarker s_tMarkerStay = new ProfilerMarker("LegacyBruteForceBrute.OnStay");

    private struct tColliderPair
    {
        public LegacyBruteForceColliderBrute ColliderA;
        public LegacyBruteForceColliderBrute ColliderB;
    }

    private static readonly Func<LegacyBruteForceColliderBrute, LegacyBruteForceColliderBrute, bool>[,] s_arrNarrowPhase =
    {
        { IsCircleCircleOverlap, IsCircleBoxOverlapPair },
        { IsBoxCircleOverlapPair, IsBoxBoxOverlap },
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        m_arrCollider = new List<LegacyBruteForceColliderBrute>[32];
        for (int i = 0; i < 32; ++i)
            m_arrCollider[i] = new List<LegacyBruteForceColliderBrute>();

        m_listPendingDelete = new List<LegacyBruteForceColliderBrute>();
        m_listIndexInLayerList = new List<int>();
        m_listOther = new List<HashSet<int>>();
        m_hashPairInfo = new Dictionary<long, tColliderPair>();
    }

    // 스포너가 씬 시작 시 한 번 호출 - Bullet↔Obstacle, Bullet↔Monster만 켠다
    public void ConfigureLayerMatrix(int _iBulletLayer, int _iObstacleLayer, int _iMonsterLayer)
    {
        m_arrLayerCollisionMatrix[_iBulletLayer] = (1 << _iObstacleLayer) | (1 << _iMonsterLayer);
    }

    private static long MakePairKey(int _iA, int _iB)
    {
        int iLow = _iA < _iB ? _iA : _iB;
        int iHigh = _iA < _iB ? _iB : _iA;
        return ((long)iLow << 32) | (uint)iHigh;
    }

    private bool IsLayerCollider(int _iLayerA, int _iLayerB)
    {
        bool bAToB = (m_arrLayerCollisionMatrix[_iLayerA].value & (1 << _iLayerB)) != 0;
        bool bBToA = (m_arrLayerCollisionMatrix[_iLayerB].value & (1 << _iLayerA)) != 0;
        return bAToB || bBToA;
    }

    private void ResizeCapacity(int _iId)
    {
        while (m_listIndexInLayerList.Count <= _iId)
        {
            m_listIndexInLayerList.Add(-1);
            m_listOther.Add(new HashSet<int>());
        }
    }

    public void RegisterCollider(LegacyBruteForceColliderBrute _refCollider)
    {
        ResizeCapacity(_refCollider.ID);
    }

    public void Activate(LegacyBruteForceColliderBrute _refCollider)
    {
        ResizeCapacity(_refCollider.ID);

        if (m_listIndexInLayerList[_refCollider.ID] >= 0)
            return;

        List<LegacyBruteForceColliderBrute> listLayer = m_arrCollider[_refCollider.Layer];
        m_listIndexInLayerList[_refCollider.ID] = listLayer.Count;
        listLayer.Add(_refCollider);
    }

    public void UnActivate(LegacyBruteForceColliderBrute _refCollider)
    {
        m_listPendingDelete.Add(_refCollider);
    }

    private void DeleteCollider(LegacyBruteForceColliderBrute _refCollider)
    {
        int iId = _refCollider.ID;
        int iMyIndex = m_listIndexInLayerList[iId];
        if (iMyIndex < 0)
            return;

        HashSet<int> hashOther = m_listOther[iId];
        foreach (int iOtherId in hashOther)
        {
            long lKey = MakePairKey(iId, iOtherId);
            if (m_hashPairInfo.TryGetValue(lKey, out tColliderPair tPair))
            {
                tPair.ColliderA.OnExitCollider(tPair.ColliderB);
                tPair.ColliderB.OnExitCollider(tPair.ColliderA);
                m_hashPairInfo.Remove(lKey);
            }
            m_listOther[iOtherId].Remove(iId);
        }
        hashOther.Clear();

        List<LegacyBruteForceColliderBrute> listLayer = m_arrCollider[_refCollider.Layer];
        int iLastIndex = listLayer.Count - 1;

        LegacyBruteForceColliderBrute refMoved = listLayer[iLastIndex];
        listLayer[iMyIndex] = refMoved;
        listLayer.RemoveAt(iLastIndex);
        m_listIndexInLayerList[refMoved.ID] = iMyIndex;
        m_listIndexInLayerList[iId] = -1;
    }

    private void Update()
    {
        for (int i = 0; i < m_listPendingDelete.Count; ++i)
        {
            LegacyBruteForceColliderBrute refCollider = m_listPendingDelete[i];
            if (refCollider.gameObject.activeInHierarchy == false)
                DeleteCollider(refCollider);
        }
        m_listPendingDelete.Clear();
    }

    private void LateUpdate()
    {
        using (s_tMarkerLateUpdate.Auto())
        {
            CheckOverlaps();
        }
    }

    private void CheckOverlaps()
    {
        PreLoadCenter();

        for (int iLayerA = 0; iLayerA < 32; ++iLayerA)
        {
            List<LegacyBruteForceColliderBrute> listA = m_arrCollider[iLayerA];
            if (listA.Count == 0)
                continue;

            for (int iLayerB = iLayerA; iLayerB < 32; ++iLayerB)
            {
                if (!IsLayerCollider(iLayerA, iLayerB))
                    continue;

                List<LegacyBruteForceColliderBrute> listB = m_arrCollider[iLayerB];
                if (listB.Count == 0)
                    continue;

                Func<LegacyBruteForceColliderBrute, LegacyBruteForceColliderBrute, bool> fnOverlap =
                    s_arrNarrowPhase[(int)listA[0].Shape, (int)listB[0].Shape];

                if (iLayerA == iLayerB)
                    CheckSameLayer(listA, fnOverlap);
                else
                    CheckCrossLayer(listA, listB, fnOverlap);
            }
        }
    }

    private void PreLoadCenter()
    {
        using (s_tMarkerRefreshCenter.Auto())
        {
            for (int iLayer = 0; iLayer < 32; ++iLayer)
            {
                List<LegacyBruteForceColliderBrute> listLayer = m_arrCollider[iLayer];
                for (int i = 0; i < listLayer.Count; ++i)
                    listLayer[i].RefreshCenter();
            }
        }
    }

    private void CheckSameLayer(List<LegacyBruteForceColliderBrute> _listCollider, Func<LegacyBruteForceColliderBrute, LegacyBruteForceColliderBrute, bool> _fnOverlap)
    {
        using (s_tMarkerSameLayer.Auto())
        {
            for (int a = 0; a < _listCollider.Count; ++a)
            {
                LegacyBruteForceColliderBrute refI = _listCollider[a];
                for (int b = a + 1; b < _listCollider.Count; ++b)
                    CheckPair(refI, _listCollider[b], _fnOverlap);
            }
        }
    }

    private void CheckCrossLayer(List<LegacyBruteForceColliderBrute> _listA, List<LegacyBruteForceColliderBrute> _listB, Func<LegacyBruteForceColliderBrute, LegacyBruteForceColliderBrute, bool> _fnOverlap)
    {
        using (s_tMarkerCrossLayer.Auto())
        {
            for (int a = 0; a < _listA.Count; ++a)
            {
                LegacyBruteForceColliderBrute refA = _listA[a];
                for (int b = 0; b < _listB.Count; ++b)
                    CheckPair(refA, _listB[b], _fnOverlap);
            }
        }
    }

    private void CheckPair(LegacyBruteForceColliderBrute _refA, LegacyBruteForceColliderBrute _refB, Func<LegacyBruteForceColliderBrute, LegacyBruteForceColliderBrute, bool> _fnOverlap)
    {
        bool bOverlapping = _fnOverlap(_refA, _refB);

        if (bOverlapping)
        {
            long lKey = MakePairKey(_refA.ID, _refB.ID);
            if (m_hashPairInfo.TryGetValue(lKey, out tColliderPair tPair))
            {
                using (s_tMarkerStay.Auto())
                {
                    tPair.ColliderA.OnStayCollider(tPair.ColliderB);
                    tPair.ColliderB.OnStayCollider(tPair.ColliderA);
                }
            }
            else
            {
                tPair = new tColliderPair { ColliderA = _refA, ColliderB = _refB };
                m_hashPairInfo.Add(lKey, tPair);
                m_listOther[_refA.ID].Add(_refB.ID);
                m_listOther[_refB.ID].Add(_refA.ID);

                tPair.ColliderA.OnEnterCollider(tPair.ColliderB);
                tPair.ColliderB.OnEnterCollider(tPair.ColliderA);
            }
        }
        else
        {
            // 안 겹치는 쌍이 압도적으로 많은 구조라, _refA가 지금 아무와도 안 겹치는
            // 중이면 이 쌍도 당연히 기록이 없다는 뜻이므로 Dictionary 조회 자체를 스킵
            if (m_listOther[_refA.ID].Count == 0)
                return;

            long lKey = MakePairKey(_refA.ID, _refB.ID);
            if (m_hashPairInfo.TryGetValue(lKey, out tColliderPair tOld))
            {
                m_hashPairInfo.Remove(lKey);
                m_listOther[_refA.ID].Remove(_refB.ID);
                m_listOther[_refB.ID].Remove(_refA.ID);

                tOld.ColliderA.OnExitCollider(tOld.ColliderB);
                tOld.ColliderB.OnExitCollider(tOld.ColliderA);
            }
        }
    }

    // ---- 도형별 겹침 판정 (421d1b0 원본과 동일) ----

    private static bool IsCircleCircleOverlap(LegacyBruteForceColliderBrute _refA, LegacyBruteForceColliderBrute _refB)
    {
        LegacyBruteForceCircleColliderBrute refCircleA = (LegacyBruteForceCircleColliderBrute)_refA;
        LegacyBruteForceCircleColliderBrute refCircleB = (LegacyBruteForceCircleColliderBrute)_refB;

        Vector3 vDelta = refCircleB.CachedCenter - refCircleA.CachedCenter;
        float fDistSq = vDelta.sqrMagnitude;
        float fRadiusSum = refCircleA.Radius + refCircleB.Radius;
        return fDistSq <= fRadiusSum * fRadiusSum;
    }

    private static bool IsCircleBoxOverlap(
        Vector3 _vSphereCenter, float _fRadius,
        Vector3 _vBoxCenter, Vector3 _vAxisX, Vector3 _vAxisY, Vector3 _vAxisZ, Vector3 _vHalfExtent)
    {
        Vector3 vDelta = _vSphereCenter - _vBoxCenter;

        float fDx = Vector3.Dot(vDelta, _vAxisX);
        float fDy = Vector3.Dot(vDelta, _vAxisY);
        float fDz = Vector3.Dot(vDelta, _vAxisZ);

        float fCx = Mathf.Clamp(fDx, -_vHalfExtent.x, _vHalfExtent.x);
        float fCy = Mathf.Clamp(fDy, -_vHalfExtent.y, _vHalfExtent.y);
        float fCz = Mathf.Clamp(fDz, -_vHalfExtent.z, _vHalfExtent.z);

        float fEx = fDx - fCx;
        float fEy = fDy - fCy;
        float fEz = fDz - fCz;

        return (fEx * fEx + fEy * fEy + fEz * fEz) <= _fRadius * _fRadius;
    }

    private static bool IsCircleBoxOverlapPair(LegacyBruteForceColliderBrute _refA, LegacyBruteForceColliderBrute _refB)
    {
        LegacyBruteForceCircleColliderBrute refCircle = (LegacyBruteForceCircleColliderBrute)_refA;
        LegacyBruteForceBoxColliderBrute refBox = (LegacyBruteForceBoxColliderBrute)_refB;

        float fBoundSum = refCircle.Radius + refBox.BoundingRadius;
        if ((refCircle.CachedCenter - refBox.CachedCenter).sqrMagnitude > fBoundSum * fBoundSum)
            return false;

        return IsCircleBoxOverlap(
            refCircle.CachedCenter, refCircle.Radius,
            refBox.CachedCenter, refBox.AxisX, refBox.AxisY, refBox.AxisZ, refBox.HalfExtent);
    }

    private static bool IsBoxCircleOverlapPair(LegacyBruteForceColliderBrute _refA, LegacyBruteForceColliderBrute _refB)
    {
        return IsCircleBoxOverlapPair(_refB, _refA);
    }

    // 지금 레이어 와이어링상 절대 호출되지 않음(장애물끼리는 충돌 검사 안 함) - 원본과
    // 동일하게 안전한 스텁으로만 둔다
    private static bool IsBoxBoxOverlap(LegacyBruteForceColliderBrute _refA, LegacyBruteForceColliderBrute _refB)
    {
        return false;
    }
}
