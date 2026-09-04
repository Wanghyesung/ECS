using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;

/*///////////////////////////////////////////
       LegacyBruteForceColliderManagerUnified
목적 : "챕터 3 - Burst Job 병렬화"(daef9e3 + 44b4646 통합)를 재현하는 벤치마크 전용
       매니저. GridTestScene1(daef9e3, Box만 그리드+Job이고 Circle-Circle은 메인스레드
       브루트포스)의 다음 단계 - Circle/Box 구분 없이 활성 콜라이더 전부를 하나의
       그리드 + 하나의 Job(GridOverlapJob)으로 판정한다. Owner/Query 구분이 없다 -
       콜라이더 자신이 곧 조회 주체이고, Execute(index)가 이웃 27칸에서 candidate
       index가 자기 이하면 스킵(j<=index)하는 규칙 하나로 중복 없는 순회를 보장한다
       (BaseCollider/ColliderManager 최종본의 GridOverlapJob과 동일 설계).

       중요: 이 챕터는 아직 "실행 순서 분리"(챕터 4, 4fcbb13+284bec5) 이전이라 Schedule
       직후 바로 Complete를 부른다 - Job이 워커에서 도는 동안 메인스레드가 겹쳐서 할
       다른 일이 없다(daef9e3에선 Circle-Circle 브루트포스가 그 몫이었지만, 이제
       그것마저 Job 안으로 흡수됐기 때문). 그래서 이 단계는 실측상 daef9e3보다
       오히려 느리게 나올 수 있다 - 이게 정상이고, 그게 챕터 4로 넘어가는 이유다
       (Docs/Collider.md의 "통합 직후 LateUpdate 11.23ms로 일시 증가" 그대로 재현).

       PreLoadCenter는 아직 메인스레드 순차 읽기다 - TransformAccessArray는 챕터 4.
 *///////////////////////////////////////////
[DefaultExecutionOrder(1000)]
public sealed class LegacyBruteForceColliderManagerUnified : MonoBehaviour
{
    private const int LAYER_COUNT = 32;
    private const int JOB_BATCH_SIZE = 64;
    private const int INITIAL_SOA_CAPACITY = 256;
    private const int COLLIDER_TYPE_CIRCLE = 0;
    private const int COLLIDER_TYPE_BOX = 1;

    public static LegacyBruteForceColliderManagerUnified Instance { get; private set; }

    private readonly LayerMask[] m_arrLayerCollisionMatrix = new LayerMask[LAYER_COUNT];
    private NativeArray<int> m_arrLayerMatrixNative;

    private List<LegacyBruteForceColliderUnified>[] m_arrCollider;
    private List<LegacyBruteForceColliderUnified> m_listPendingDelete;
    private List<int> m_listIndexInLayerList;
    private List<LegacyBruteForceColliderUnified> m_listColliderByID;
    private List<HashSet<int>> m_listOther;
    private Dictionary<long, tColliderPair> m_hashPairInfo;

    // 매 프레임 활성 콜라이더 전부를 모으는 스크래치 - 44b4646 시점 원본 그대로 List 기반
    // (이 O(N) 복사를 없애는 최적화는 이후 세션 과제라 이 챕터에선 그대로 둔다)
    private readonly List<LegacyBruteForceColliderUnified> m_listAllActive = new();

    private readonly LegacyColliderGridUnified m_grid = new();

    private NativeArray<Vector3> m_arrCenter;
    private NativeArray<Vector3> m_arrAxisX;
    private NativeArray<Vector3> m_arrAxisY;
    private NativeArray<Vector3> m_arrAxisZ;
    private NativeArray<Vector3> m_arrHalfExtent;
    private NativeArray<float> m_arrBoundingRadius;
    private NativeArray<int> m_arrColliderId;
    private NativeArray<int> m_arrColliderType;
    private NativeArray<int> m_arrLayerOf;
    private NativeArray<bool> m_arrHasPair;
    private int m_iCount;

    private NativeQueue<tPairResult> m_queResult;
    private JobHandle m_tJobHandle;
    private bool m_bScheduled;

    private static readonly ProfilerMarker s_tMarkerLateUpdate = new ProfilerMarker("LegacyBruteForceUnified.LateUpdate");
    private static readonly ProfilerMarker s_tMarkerRefreshCenter = new ProfilerMarker("LegacyBruteForceUnified.PreLoadCenter");
    private static readonly ProfilerMarker s_tMarkerBuildGrid = new ProfilerMarker("LegacyBruteForceUnified.BuildGrid");
    private static readonly ProfilerMarker s_tMarkerSchedule = new ProfilerMarker("LegacyBruteForceUnified.GridJobSchedule");
    private static readonly ProfilerMarker s_tMarkerComplete = new ProfilerMarker("LegacyBruteForceUnified.GridJobComplete");
    private static readonly ProfilerMarker s_tMarkerDrain = new ProfilerMarker("LegacyBruteForceUnified.GridJobDrain");
    private static readonly ProfilerMarker s_tMarkerStay = new ProfilerMarker("LegacyBruteForceUnified.OnStay");

    private struct tColliderPair
    {
        public LegacyBruteForceColliderUnified ColliderA;
        public LegacyBruteForceColliderUnified ColliderB;
    }

    private struct tPairResult
    {
        public int IdA;
        public int IdB;
        public bool Overlap;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        m_arrCollider = new List<LegacyBruteForceColliderUnified>[LAYER_COUNT];
        for (int i = 0; i < LAYER_COUNT; ++i)
            m_arrCollider[i] = new List<LegacyBruteForceColliderUnified>();

        m_listPendingDelete = new List<LegacyBruteForceColliderUnified>();
        m_listIndexInLayerList = new List<int>();
        m_listColliderByID = new List<LegacyBruteForceColliderUnified>();
        m_listOther = new List<HashSet<int>>();
        m_hashPairInfo = new Dictionary<long, tColliderPair>();

        m_arrLayerMatrixNative = new NativeArray<int>(LAYER_COUNT, Allocator.Persistent);
        m_queResult = new NativeQueue<tPairResult>(Allocator.Persistent);
    }

    private void OnDestroy()
    {
        if (m_bScheduled)
        {
            m_tJobHandle.Complete();
            m_bScheduled = false;
        }

        m_grid.Dispose();

        DisposeIfCreated(m_arrCenter);
        DisposeIfCreated(m_arrAxisX);
        DisposeIfCreated(m_arrAxisY);
        DisposeIfCreated(m_arrAxisZ);
        DisposeIfCreated(m_arrHalfExtent);
        DisposeIfCreated(m_arrBoundingRadius);
        DisposeIfCreated(m_arrColliderId);
        DisposeIfCreated(m_arrColliderType);
        DisposeIfCreated(m_arrLayerOf);
        DisposeIfCreated(m_arrHasPair);

        if (m_arrLayerMatrixNative.IsCreated)
            m_arrLayerMatrixNative.Dispose();
        if (m_queResult.IsCreated)
            m_queResult.Dispose();
    }

    private static void DisposeIfCreated<T>(NativeArray<T> _arr) where T : struct
    {
        if (_arr.IsCreated)
            _arr.Dispose();
    }

    public void ConfigureLayerMatrix(int _iBulletLayer, int _iObstacleLayer, int _iMonsterLayer)
    {
        m_arrLayerCollisionMatrix[_iBulletLayer] = (1 << _iObstacleLayer) | (1 << _iMonsterLayer);

        for (int i = 0; i < LAYER_COUNT; ++i)
            m_arrLayerMatrixNative[i] = m_arrLayerCollisionMatrix[i].value;
    }

    private static long MakePairKey(int _iA, int _iB)
    {
        int iLow = _iA < _iB ? _iA : _iB;
        int iHigh = _iA < _iB ? _iB : _iA;
        return ((long)iLow << 32) | (uint)iHigh;
    }

    private void ResizeCapacity(int _iId)
    {
        while (m_listIndexInLayerList.Count <= _iId)
        {
            m_listIndexInLayerList.Add(-1);
            m_listOther.Add(new HashSet<int>());
            m_listColliderByID.Add(null);
        }
    }

    public void RegisterCollider(LegacyBruteForceColliderUnified _refCollider)
    {
        ResizeCapacity(_refCollider.ID);
        m_listColliderByID[_refCollider.ID] = _refCollider;
    }

    public void Activate(LegacyBruteForceColliderUnified _refCollider)
    {
        ResizeCapacity(_refCollider.ID);
        m_listColliderByID[_refCollider.ID] = _refCollider;

        if (m_listIndexInLayerList[_refCollider.ID] >= 0)
            return;

        List<LegacyBruteForceColliderUnified> listLayer = m_arrCollider[_refCollider.Layer];
        m_listIndexInLayerList[_refCollider.ID] = listLayer.Count;
        listLayer.Add(_refCollider);
    }

    public void UnActivate(LegacyBruteForceColliderUnified _refCollider)
    {
        m_listPendingDelete.Add(_refCollider);
    }

    private void DeleteCollider(LegacyBruteForceColliderUnified _refCollider)
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

        List<LegacyBruteForceColliderUnified> listLayer = m_arrCollider[_refCollider.Layer];
        int iLastIndex = listLayer.Count - 1;

        LegacyBruteForceColliderUnified refMoved = listLayer[iLastIndex];
        listLayer[iMyIndex] = refMoved;
        listLayer.RemoveAt(iLastIndex);
        m_listIndexInLayerList[refMoved.ID] = iMyIndex;
        m_listIndexInLayerList[iId] = -1;
    }

    private void Update()
    {
        for (int i = 0; i < m_listPendingDelete.Count; ++i)
        {
            LegacyBruteForceColliderUnified refCollider = m_listPendingDelete[i];
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

    // 챕터 4 이전이라 Schedule 직후 바로 Complete - 겹쳐 돌 다른 작업이 없다(의도적)
    private void CheckOverlaps()
    {
        PreLoadCenter();
        BuildGrid();

        if (ScheduleJob())
        {
            using (s_tMarkerComplete.Auto())
            {
                m_tJobHandle.Complete();
            }
            m_bScheduled = false;

            DrainResults();
        }
    }

    private void PreLoadCenter()
    {
        using (s_tMarkerRefreshCenter.Auto())
        {
            for (int iLayer = 0; iLayer < LAYER_COUNT; ++iLayer)
            {
                List<LegacyBruteForceColliderUnified> listLayer = m_arrCollider[iLayer];
                for (int i = 0; i < listLayer.Count; ++i)
                    listLayer[i].RefreshCenter();
            }
        }
    }

    // 활성 콜라이더 전부(도형/레이어 무관)를 하나의 List로 모으고, SoA + 그리드를 채운다
    private void BuildGrid()
    {
        using (s_tMarkerBuildGrid.Auto())
        {
            m_listAllActive.Clear();
            for (int iLayer = 0; iLayer < LAYER_COUNT; ++iLayer)
            {
                List<LegacyBruteForceColliderUnified> listLayer = m_arrCollider[iLayer];
                for (int i = 0; i < listLayer.Count; ++i)
                    m_listAllActive.Add(listLayer[i]);
            }

            m_iCount = 0;
            if (m_listAllActive.Count == 0)
                return;

            if (m_grid.IsBuilt == false)
                m_grid.Build(m_listAllActive);
            if (m_grid.IsBuilt == false)
                return;

            EnsureCapacity(m_listAllActive.Count);
            m_grid.BeginRebuild(m_listAllActive.Count);

            for (int i = 0; i < m_listAllActive.Count; ++i)
            {
                LegacyBruteForceColliderUnified refCollider = m_listAllActive[i];
                Vector3 vCenter = refCollider.CachedCenter;

                m_arrCenter[i] = vCenter;
                m_arrBoundingRadius[i] = refCollider.BoundingRadius;
                m_arrColliderId[i] = refCollider.ID;
                m_arrColliderType[i] = (int)refCollider.Shape;
                m_arrLayerOf[i] = refCollider.Layer;
                m_arrHasPair[i] = m_listOther[refCollider.ID].Count > 0;

                if (refCollider.Shape == eLegacyColliderShapeUnified.Box)
                {
                    LegacyBruteForceBoxColliderUnified refBox = (LegacyBruteForceBoxColliderUnified)refCollider;
                    m_arrAxisX[i] = refBox.AxisX;
                    m_arrAxisY[i] = refBox.AxisY;
                    m_arrAxisZ[i] = refBox.AxisZ;
                    m_arrHalfExtent[i] = refBox.HalfExtent;
                }

                m_grid.AddItem(i, vCenter);
            }

            m_grid.EndRebuild();
            m_iCount = m_listAllActive.Count;
        }
    }

    private bool ScheduleJob()
    {
        using (s_tMarkerSchedule.Auto())
        {
            if (m_grid.IsBuilt == false || m_iCount == 0)
                return false;

            GridOverlapJob tJob = new GridOverlapJob
            {
                CellStart = m_grid.CellStart,
                CellCount = m_grid.CellCount,
                CellItems = m_grid.CellItems,
                GridOrigin = m_grid.Origin,
                CellSize = m_grid.CellSize,
                CountX = m_grid.CountX,
                CountY = m_grid.CountY,
                CountZ = m_grid.CountZ,

                Center = m_arrCenter,
                AxisX = m_arrAxisX,
                AxisY = m_arrAxisY,
                AxisZ = m_arrAxisZ,
                HalfExtent = m_arrHalfExtent,
                BoundingRadius = m_arrBoundingRadius,
                ColliderId = m_arrColliderId,
                ColliderType = m_arrColliderType,
                Layer = m_arrLayerOf,
                HasPair = m_arrHasPair,
                LayerMatrixValue = m_arrLayerMatrixNative,

                Output = m_queResult.AsParallelWriter()
            };

            m_tJobHandle = tJob.Schedule(m_iCount, JOB_BATCH_SIZE);
            m_bScheduled = true;

            JobHandle.ScheduleBatchedJobs();
            return true;
        }
    }

    private void DrainResults()
    {
        using (s_tMarkerDrain.Auto())
        {
            while (m_queResult.TryDequeue(out tPairResult tResult))
            {
                LegacyBruteForceColliderUnified refA = GetColliderByID(tResult.IdA);
                LegacyBruteForceColliderUnified refB = GetColliderByID(tResult.IdB);

                if (refA == null || refB == null)
                    continue;

                CheckPair(refA, refB, tResult.Overlap);
            }
        }
    }

    private LegacyBruteForceColliderUnified GetColliderByID(int _iId)
    {
        if (_iId < 0 || _iId >= m_listColliderByID.Count)
            return null;

        return m_listColliderByID[_iId];
    }

    private void CheckPair(LegacyBruteForceColliderUnified _refA, LegacyBruteForceColliderUnified _refB, bool _bOverlapping)
    {
        if (_bOverlapping)
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

    // ---- SoA 용량 관리 ----

    private void EnsureCapacity(int _iCount)
    {
        if (m_arrCenter.IsCreated && m_arrCenter.Length >= _iCount)
            return;

        int iNewCapacity = m_arrCenter.IsCreated ? m_arrCenter.Length : INITIAL_SOA_CAPACITY;
        while (iNewCapacity < _iCount)
            iNewCapacity <<= 1;

        DisposeIfCreated(m_arrCenter);
        DisposeIfCreated(m_arrAxisX);
        DisposeIfCreated(m_arrAxisY);
        DisposeIfCreated(m_arrAxisZ);
        DisposeIfCreated(m_arrHalfExtent);
        DisposeIfCreated(m_arrBoundingRadius);
        DisposeIfCreated(m_arrColliderId);
        DisposeIfCreated(m_arrColliderType);
        DisposeIfCreated(m_arrLayerOf);
        DisposeIfCreated(m_arrHasPair);

        m_arrCenter = new NativeArray<Vector3>(iNewCapacity, Allocator.Persistent);
        m_arrAxisX = new NativeArray<Vector3>(iNewCapacity, Allocator.Persistent);
        m_arrAxisY = new NativeArray<Vector3>(iNewCapacity, Allocator.Persistent);
        m_arrAxisZ = new NativeArray<Vector3>(iNewCapacity, Allocator.Persistent);
        m_arrHalfExtent = new NativeArray<Vector3>(iNewCapacity, Allocator.Persistent);
        m_arrBoundingRadius = new NativeArray<float>(iNewCapacity, Allocator.Persistent);
        m_arrColliderId = new NativeArray<int>(iNewCapacity, Allocator.Persistent);
        m_arrColliderType = new NativeArray<int>(iNewCapacity, Allocator.Persistent);
        m_arrLayerOf = new NativeArray<int>(iNewCapacity, Allocator.Persistent);
        m_arrHasPair = new NativeArray<bool>(iNewCapacity, Allocator.Persistent);
    }

    // 원(구)-OBB 판정 순수 함수 - Job이 Burst로 컴파일해 그대로 호출한다
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

    private static bool IsCircleCircleOverlap(Vector3 _vCenterA, float _fRadiusA, Vector3 _vCenterB, float _fRadiusB)
    {
        Vector3 vDelta = _vCenterB - _vCenterA;
        float fRadiusSum = _fRadiusA + _fRadiusB;
        return vDelta.sqrMagnitude <= fRadiusSum * fRadiusSum;
    }

    private static bool IsLayerCollider(NativeArray<int> _arrMatrix, int _iLayerA, int _iLayerB)
    {
        bool bAToB = (_arrMatrix[_iLayerA] & (1 << _iLayerB)) != 0;
        bool bBToA = (_arrMatrix[_iLayerB] & (1 << _iLayerA)) != 0;
        return bAToB || bBToA;
    }

    // ---- Job ----

    // 그리드에 들어간 콜라이더 하나(index)당 이웃 27칸의 후보만 검사한다. candidate index가
    // 자기 이하면 스킵(자기 자신이거나 이미 반대 방향에서 검사된 쌍) - 이 규칙 하나로
    // "그리드 소유/조회" 구분이나 레이어별 중복 방지 가드 없이 중복 없는 순회가 된다
    [BurstCompile]
    private struct GridOverlapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> CellStart;
        [ReadOnly] public NativeArray<int> CellCount;
        [ReadOnly] public NativeArray<int> CellItems;

        public Vector3 GridOrigin;
        public float CellSize;
        public int CountX;
        public int CountY;
        public int CountZ;

        [ReadOnly] public NativeArray<Vector3> Center;
        [ReadOnly] public NativeArray<Vector3> AxisX;
        [ReadOnly] public NativeArray<Vector3> AxisY;
        [ReadOnly] public NativeArray<Vector3> AxisZ;
        [ReadOnly] public NativeArray<Vector3> HalfExtent;
        [ReadOnly] public NativeArray<float> BoundingRadius;
        [ReadOnly] public NativeArray<int> ColliderId;
        [ReadOnly] public NativeArray<int> ColliderType;
        [ReadOnly] public NativeArray<int> Layer;
        [ReadOnly] public NativeArray<bool> HasPair;

        [ReadOnly] public NativeArray<int> LayerMatrixValue;

        public NativeQueue<tPairResult>.ParallelWriter Output;

        public void Execute(int index)
        {
            Vector3 vMyCenter = Center[index];
            float fMyRadius = BoundingRadius[index];
            int iMyType = ColliderType[index];
            int iMyLayer = Layer[index];
            int iMyId = ColliderId[index];
            bool bMyHasPair = HasPair[index];

            LegacyColliderGridUnified.ComputeCellCoord(vMyCenter, GridOrigin, CellSize, CountX, CountY, CountZ,
                out int iCX, out int iCY, out int iCZ);

            int iMinX = iCX > 0 ? iCX - 1 : 0;
            int iMaxX = iCX < CountX - 1 ? iCX + 1 : CountX - 1;
            int iMinY = iCY > 0 ? iCY - 1 : 0;
            int iMaxY = iCY < CountY - 1 ? iCY + 1 : CountY - 1;
            int iMinZ = iCZ > 0 ? iCZ - 1 : 0;
            int iMaxZ = iCZ < CountZ - 1 ? iCZ + 1 : CountZ - 1;

            for (int ix = iMinX; ix <= iMaxX; ++ix)
                for (int iy = iMinY; iy <= iMaxY; ++iy)
                    for (int iz = iMinZ; iz <= iMaxZ; ++iz)
                    {
                        int iCell = LegacyColliderGridUnified.FlattenIndex(ix, iy, iz, CountX, CountY);
                        int iStart = CellStart[iCell];
                        int iEnd = iStart + CellCount[iCell];

                        for (int k = iStart; k < iEnd; ++k)
                        {
                            int j = CellItems[k];

                            if (j <= index)
                                continue;

                            if (!IsLayerCollider(LayerMatrixValue, iMyLayer, Layer[j]))
                                continue;

                            int iOtherType = ColliderType[j];
                            Vector3 vOtherCenter = Center[j];
                            float fOtherRadius = BoundingRadius[j];

                            bool bOverlap;
                            if (iMyType != COLLIDER_TYPE_BOX && iOtherType != COLLIDER_TYPE_BOX)
                                bOverlap = LegacyBruteForceColliderManagerUnified.IsCircleCircleOverlap(vMyCenter, fMyRadius, vOtherCenter, fOtherRadius);
                            else if (iMyType == COLLIDER_TYPE_BOX && iOtherType == COLLIDER_TYPE_BOX)
                                bOverlap = false; // Box-Box (지금 매트릭스엔 없음, 방어적 스텁)
                            else
                            {
                                float fBoundSum = fMyRadius + fOtherRadius;
                                Vector3 vDelta = vOtherCenter - vMyCenter;
                                bOverlap = false;

                                if (vDelta.sqrMagnitude <= fBoundSum * fBoundSum)
                                {
                                    bOverlap = iMyType == COLLIDER_TYPE_BOX
                                        ? LegacyBruteForceColliderManagerUnified.IsCircleBoxOverlap(
                                            vOtherCenter, fOtherRadius, vMyCenter, AxisX[index], AxisY[index], AxisZ[index], HalfExtent[index])
                                        : LegacyBruteForceColliderManagerUnified.IsCircleBoxOverlap(
                                            vMyCenter, fMyRadius, vOtherCenter, AxisX[j], AxisY[j], AxisZ[j], HalfExtent[j]);
                                }
                            }

                            if (bOverlap || bMyHasPair || HasPair[j])
                            {
                                Output.Enqueue(new tPairResult
                                {
                                    IdA = iMyId,
                                    IdB = ColliderId[j],
                                    Overlap = bOverlap
                                });
                            }
                        }
                    }
        }
    }
}
