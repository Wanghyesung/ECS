using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

/*///////////////////////////////////////////
        LegacyBruteForceColliderManager
목적 : 커밋 421d1b0(그리드 도입 직전) → 8edea44(Box 그리드 도입) 두 단계를 이어서
       재현하는 벤치마크 전용 매니저. Box(Obstacle) 레이어만 LegacyBoxColliderGrid로
       브로드페이즈를 걸고, Circle-Circle(총알↔몬스터)은 8edea44 시점 그대로 아직
       브루트포스다 - Circle-Circle까지 그리드로 통합되는 건 훨씬 뒤 44b4646.
       판정 로직(Dictionary 기반 Enter/Stay/Exit, 레이어 매트릭스, 도형별 디스패치)은
       원본과 100% 동일하고, RaycastMask/FindAllInRadius/FindNearest 같은 조회 API만
       뺐다 - 이 벤치마크가 재는 건 판정 비용이지 쿼리 비용이 아니므로.

       [DefaultExecutionOrder(1000)]는 원본 그대로 유지했지만, 이 벤치마크의
       LegacyBruteForceMover는 Update()에서 직접 이동해서 Unity 프레임 구조상
       모든 Update()가 이미 이 매니저의 LateUpdate()보다 먼저 끝나므로 정확성엔
       영향이 없다 - 원본과 똑같은 실행 순서 아래서 측정한다는 의미로 남겨둔다.
 *///////////////////////////////////////////
[DefaultExecutionOrder(1000)]
public sealed class LegacyBruteForceColliderManager : MonoBehaviour
{
    public static LegacyBruteForceColliderManager Instance { get; private set; }

    private readonly LayerMask[] m_arrLayerCollisionMatrix = new LayerMask[32];

    private List<LegacyBruteForceCollider>[] m_arrCollider;
    private List<LegacyBruteForceCollider> m_listPendingDelete;
    private List<int> m_listIndexInLayerList;
    private List<HashSet<int>> m_listOther;
    private Dictionary<long, tColliderPair> m_hashPairInfo;
    private readonly LegacyBoxColliderGrid m_gridBox = new LegacyBoxColliderGrid();

    private static readonly ProfilerMarker s_tMarkerLateUpdate = new ProfilerMarker("LegacyBruteForce.LateUpdate");
    private static readonly ProfilerMarker s_tMarkerRefreshCenter = new ProfilerMarker("LegacyBruteForce.PreLoadCenter");
    private static readonly ProfilerMarker s_tMarkerSameLayer = new ProfilerMarker("LegacyBruteForce.CheckSameLayer");
    private static readonly ProfilerMarker s_tMarkerCrossLayer = new ProfilerMarker("LegacyBruteForce.CheckCrossLayer");
    private static readonly ProfilerMarker s_tMarkerCrossLayerGrid = new ProfilerMarker("LegacyBruteForce.CheckCrossLayerGrid");
    private static readonly ProfilerMarker s_tMarkerStay = new ProfilerMarker("LegacyBruteForce.OnStay");

    private struct tColliderPair
    {
        public LegacyBruteForceCollider ColliderA;
        public LegacyBruteForceCollider ColliderB;
    }

    private static readonly Func<LegacyBruteForceCollider, LegacyBruteForceCollider, bool>[,] s_arrNarrowPhase =
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

        m_arrCollider = new List<LegacyBruteForceCollider>[32];
        for (int i = 0; i < 32; ++i)
            m_arrCollider[i] = new List<LegacyBruteForceCollider>();

        m_listPendingDelete = new List<LegacyBruteForceCollider>();
        m_listIndexInLayerList = new List<int>();
        m_listOther = new List<HashSet<int>>();
        m_hashPairInfo = new Dictionary<long, tColliderPair>();
    }

    // 스포너가 씬 시작 시 한 번 호출 - 이 벤치마크에서 실제로 충돌해야 하는 레이어
    // 쌍만 켠다(Bullet↔Obstacle, Bullet↔Monster). 인자 레이어 번호는
    // LegacyBruteForceSpawner의 상수와 반드시 일치해야 한다
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

    public void RegisterCollider(LegacyBruteForceCollider _refCollider)
    {
        ResizeCapacity(_refCollider.ID);
    }

    public void Activate(LegacyBruteForceCollider _refCollider)
    {
        ResizeCapacity(_refCollider.ID);

        if (m_listIndexInLayerList[_refCollider.ID] >= 0)
            return;

        List<LegacyBruteForceCollider> listLayer = m_arrCollider[_refCollider.Layer];
        m_listIndexInLayerList[_refCollider.ID] = listLayer.Count;
        listLayer.Add(_refCollider);
    }

    public void UnActivate(LegacyBruteForceCollider _refCollider)
    {
        m_listPendingDelete.Add(_refCollider);
    }

    private void DeleteCollider(LegacyBruteForceCollider _refCollider)
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

        List<LegacyBruteForceCollider> listLayer = m_arrCollider[_refCollider.Layer];
        int iLastIndex = listLayer.Count - 1;

        LegacyBruteForceCollider refMoved = listLayer[iLastIndex];
        listLayer[iMyIndex] = refMoved;
        listLayer.RemoveAt(iLastIndex);
        m_listIndexInLayerList[refMoved.ID] = iMyIndex;
        m_listIndexInLayerList[iId] = -1;
    }

    private void Update()
    {
        for (int i = 0; i < m_listPendingDelete.Count; ++i)
        {
            LegacyBruteForceCollider refCollider = m_listPendingDelete[i];
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
            List<LegacyBruteForceCollider> listA = m_arrCollider[iLayerA];
            if (listA.Count == 0)
                continue;

            for (int iLayerB = iLayerA; iLayerB < 32; ++iLayerB)
            {
                if (!IsLayerCollider(iLayerA, iLayerB))
                    continue;

                List<LegacyBruteForceCollider> listB = m_arrCollider[iLayerB];
                if (listB.Count == 0)
                    continue;

                Func<LegacyBruteForceCollider, LegacyBruteForceCollider, bool> fnOverlap =
                    s_arrNarrowPhase[(int)listA[0].Shape, (int)listB[0].Shape];

                if (iLayerA == iLayerB)
                    CheckSameLayer(listA, fnOverlap);
                else if (listA[0].Shape == eLegacyColliderShape.Box || listB[0].Shape == eLegacyColliderShape.Box)
                    CheckCrossLayerGrid(listA, listB, fnOverlap);
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
                List<LegacyBruteForceCollider> listLayer = m_arrCollider[iLayer];
                for (int i = 0; i < listLayer.Count; ++i)
                    listLayer[i].RefreshCenter();
            }
        }
    }

    private void CheckSameLayer(List<LegacyBruteForceCollider> _listCollider, Func<LegacyBruteForceCollider, LegacyBruteForceCollider, bool> _fnOverlap)
    {
        using (s_tMarkerSameLayer.Auto())
        {
            for (int a = 0; a < _listCollider.Count; ++a)
            {
                LegacyBruteForceCollider refI = _listCollider[a];
                for (int b = a + 1; b < _listCollider.Count; ++b)
                    CheckPair(refI, _listCollider[b], _fnOverlap);
            }
        }
    }

    private void CheckCrossLayer(List<LegacyBruteForceCollider> _listA, List<LegacyBruteForceCollider> _listB, Func<LegacyBruteForceCollider, LegacyBruteForceCollider, bool> _fnOverlap)
    {
        using (s_tMarkerCrossLayer.Auto())
        {
            for (int a = 0; a < _listA.Count; ++a)
            {
                LegacyBruteForceCollider refA = _listA[a];
                for (int b = 0; b < _listB.Count; ++b)
                    CheckPair(refA, _listB[b], _fnOverlap);
            }
        }
    }

    // Box가 낀 크로스 레이어 쌍 전용 - 8edea44 도입분. Box 쪽을 그리드 소유자로 두고
    // 상대(총알 등, 개체 수가 훨씬 많은 쪽)가 자기 셀 기준 이웃 27칸의 Box 후보만 본다.
    // 그리드 자체는 Build 시점에 한 번만 구축(정적 분할)하고, 멤버십(어느 셀에 뭐가
    // 있는지)만 매 프레임 통째로 다시 채운다
    private void CheckCrossLayerGrid(List<LegacyBruteForceCollider> _listA, List<LegacyBruteForceCollider> _listB, Func<LegacyBruteForceCollider, LegacyBruteForceCollider, bool> _fnOverlap)
    {
        using (s_tMarkerCrossLayerGrid.Auto())
        {
            bool bAIsBox = _listA[0].Shape == eLegacyColliderShape.Box;
            List<LegacyBruteForceCollider> listOwner = bAIsBox ? _listA : _listB;
            List<LegacyBruteForceCollider> listOther = bAIsBox ? _listB : _listA;

            if (m_gridBox.IsBuilt == false)
                m_gridBox.Build(listOwner);

            m_gridBox.BeginRebuild();
            for (int i = 0; i < listOwner.Count; ++i)
                m_gridBox.AddCollider(listOwner[i]);

            for (int i = 0; i < listOther.Count; ++i)
            {
                LegacyBruteForceCollider refOther = listOther[i];

                LegacyBoxColliderGrid.ComputeCellCoord(refOther.CachedCenter, m_gridBox.Origin, m_gridBox.CellSize,
                    m_gridBox.CountX, m_gridBox.CountY, m_gridBox.CountZ, out int iCX, out int iCY, out int iCZ);

                int iMinX = iCX > 0 ? iCX - 1 : 0;
                int iMaxX = iCX < m_gridBox.CountX - 1 ? iCX + 1 : m_gridBox.CountX - 1;
                int iMinY = iCY > 0 ? iCY - 1 : 0;
                int iMaxY = iCY < m_gridBox.CountY - 1 ? iCY + 1 : m_gridBox.CountY - 1;
                int iMinZ = iCZ > 0 ? iCZ - 1 : 0;
                int iMaxZ = iCZ < m_gridBox.CountZ - 1 ? iCZ + 1 : m_gridBox.CountZ - 1;

                for (int ix = iMinX; ix <= iMaxX; ++ix)
                    for (int iy = iMinY; iy <= iMaxY; ++iy)
                        for (int iz = iMinZ; iz <= iMaxZ; ++iz)
                        {
                            List<LegacyBruteForceCollider> listCell = m_gridBox.GetCell(ix, iy, iz);
                            for (int k = 0; k < listCell.Count; ++k)
                            {
                                if (bAIsBox)
                                    CheckPair(listCell[k], refOther, _fnOverlap);
                                else
                                    CheckPair(refOther, listCell[k], _fnOverlap);
                            }
                        }
            }
        }
    }

    private void CheckPair(LegacyBruteForceCollider _refA, LegacyBruteForceCollider _refB, Func<LegacyBruteForceCollider, LegacyBruteForceCollider, bool> _fnOverlap)
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
            // 안 겹치는 쌍이 압도적으로 많은 구조라(총알 x 장애물/몬스터 대부분은 매 순간
            // 안 겹침), _refA가 지금 아무와도 안 겹치는 중이면 Dictionary 조회 자체를 스킵
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

    private static bool IsCircleCircleOverlap(LegacyBruteForceCollider _refA, LegacyBruteForceCollider _refB)
    {
        LegacyBruteForceCircleCollider refCircleA = (LegacyBruteForceCircleCollider)_refA;
        LegacyBruteForceCircleCollider refCircleB = (LegacyBruteForceCircleCollider)_refB;

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

    private static bool IsCircleBoxOverlapPair(LegacyBruteForceCollider _refA, LegacyBruteForceCollider _refB)
    {
        LegacyBruteForceCircleCollider refCircle = (LegacyBruteForceCircleCollider)_refA;
        LegacyBruteForceBoxCollider refBox = (LegacyBruteForceBoxCollider)_refB;

        float fBoundSum = refCircle.Radius + refBox.BoundingRadius;
        if ((refCircle.CachedCenter - refBox.CachedCenter).sqrMagnitude > fBoundSum * fBoundSum)
            return false;

        return IsCircleBoxOverlap(
            refCircle.CachedCenter, refCircle.Radius,
            refBox.CachedCenter, refBox.AxisX, refBox.AxisY, refBox.AxisZ, refBox.HalfExtent);
    }

    private static bool IsBoxCircleOverlapPair(LegacyBruteForceCollider _refA, LegacyBruteForceCollider _refB)
    {
        return IsCircleBoxOverlapPair(_refB, _refA);
    }

    // 지금 레이어 와이어링상 절대 호출되지 않음(장애물끼리는 충돌 검사 안 함) - 원본과
    // 동일하게 안전한 스텁으로만 둔다
    private static bool IsBoxBoxOverlap(LegacyBruteForceCollider _refA, LegacyBruteForceCollider _refB)
    {
        return false;
    }
}
