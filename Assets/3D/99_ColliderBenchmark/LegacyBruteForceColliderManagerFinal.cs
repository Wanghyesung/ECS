using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;

/*///////////////////////////////////////////
       LegacyBruteForceColliderManagerFinal
목적 : "챕터 4 - 스케줄링 최적화"(4fcbb13 TransformAccessArray + 284bec5 실행순서
       분리 통합)를 재현하는 벤치마크 전용 매니저. 여기까지 오면 실제 BattleScene의
       ColliderManager와 판정 알고리즘/구조가 동일해진다.

       Awake()가 같은 GameObject에 LegacyBruteForceColliderSchedulerFinal을 자동
       AddComponent한다(Docs/Collider.md §18의 실제 설계와 동일 - 씬에 수동 배치
       불필요). 역할 분담:
       - Scheduler([DefaultExecutionOrder(-1000)], 프레임 최상단): 삭제 정리 +
         Circle Transform 갱신(TransformAccessArray Job) + 그리드 재구성 +
         GridOverlapJob Schedule까지 전부 Update()에서.
       - 이 매니저([DefaultExecutionOrder(1000)], 프레임 최하단): GridOverlapJob
         Complete + 결과 드레인만 LateUpdate()에서.
       그 사이(이번 프레임 나머지 Update 전체 + LateUpdate 앞부분)만큼 Job이 실제로
       겹쳐 돌 시간을 번다 - 챕터 3에서 Schedule 직후 바로 Complete하던 것과 달리
       이제 그 창이 프레임 전체로 넓어진다.
 *///////////////////////////////////////////
[DefaultExecutionOrder(1000)]
public sealed class LegacyBruteForceColliderManagerFinal : MonoBehaviour
{
    private const int LAYER_COUNT = 32;
    private const int JOB_BATCH_SIZE = 64;
    private const int INITIAL_SOA_CAPACITY = 256;
    private const int COLLIDER_TYPE_CIRCLE = 0;
    private const int COLLIDER_TYPE_BOX = 1;

    public static LegacyBruteForceColliderManagerFinal Instance { get; private set; }

    private readonly LayerMask[] m_arrLayerCollisionMatrix = new LayerMask[LAYER_COUNT];
    private NativeArray<int> m_arrLayerMatrixNative;

    private List<LegacyBruteForceColliderFinal>[] m_arrCollider;
    private List<LegacyBruteForceColliderFinal> m_listPendingDelete;
    private List<int> m_listIndexInLayerList;
    private List<LegacyBruteForceColliderFinal> m_listColliderByID;
    private List<HashSet<int>> m_listOther;
    private Dictionary<long, tColliderPair> m_hashPairInfo;

    private readonly List<LegacyBruteForceColliderFinal> m_listAllActive = new();
    private readonly LegacyColliderGridFinal m_grid = new();

    // ---- TransformAccessArray (Circle 전용 - Box는 정적이라 등록되지 않음) ----
    private TransformAccessArray m_transformAccessArray;
    private List<int> m_listRefreshColliderIdBySlot;
    private List<int> m_listRefreshSlotByColliderId; // ColliderID -> 슬롯(-1이면 미등록)
    private NativeArray<Vector3> m_arrRefreshedCenter;

    // ---- 그리드/판정용 SoA ----
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

    private static readonly ProfilerMarker s_tMarkerLateUpdate = new ProfilerMarker("LegacyBruteForceFinal.LateUpdate");
    private static readonly ProfilerMarker s_tMarkerSchedulerUpdate = new ProfilerMarker("LegacyBruteForceFinal.SchedulerUpdate");
    private static readonly ProfilerMarker s_tMarkerRefreshCenter = new ProfilerMarker("LegacyBruteForceFinal.PreLoadCenter");
    private static readonly ProfilerMarker s_tMarkerBuildGrid = new ProfilerMarker("LegacyBruteForceFinal.BuildGrid");
    private static readonly ProfilerMarker s_tMarkerSchedule = new ProfilerMarker("LegacyBruteForceFinal.GridJobSchedule");
    private static readonly ProfilerMarker s_tMarkerComplete = new ProfilerMarker("LegacyBruteForceFinal.GridJobComplete");
    private static readonly ProfilerMarker s_tMarkerDrain = new ProfilerMarker("LegacyBruteForceFinal.GridJobDrain");
    private static readonly ProfilerMarker s_tMarkerStay = new ProfilerMarker("LegacyBruteForceFinal.OnStay");

    private struct tColliderPair
    {
        public LegacyBruteForceColliderFinal ColliderA;
        public LegacyBruteForceColliderFinal ColliderB;
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

        m_arrCollider = new List<LegacyBruteForceColliderFinal>[LAYER_COUNT];
        for (int i = 0; i < LAYER_COUNT; ++i)
            m_arrCollider[i] = new List<LegacyBruteForceColliderFinal>();

        m_listPendingDelete = new List<LegacyBruteForceColliderFinal>();
        m_listIndexInLayerList = new List<int>();
        m_listColliderByID = new List<LegacyBruteForceColliderFinal>();
        m_listOther = new List<HashSet<int>>();
        m_hashPairInfo = new Dictionary<long, tColliderPair>();

        m_listRefreshColliderIdBySlot = new List<int>();
        m_listRefreshSlotByColliderId = new List<int>();
        m_transformAccessArray = new TransformAccessArray(INITIAL_SOA_CAPACITY);

        m_arrLayerMatrixNative = new NativeArray<int>(LAYER_COUNT, Allocator.Persistent);
        m_queResult = new NativeQueue<tPairResult>(Allocator.Persistent);

        // 씬에 수동 배치 없이 -1000 스케줄러를 자동으로 붙인다
        gameObject.AddComponent<LegacyBruteForceColliderSchedulerFinal>();
    }

    private void OnDestroy()
    {
        if (m_bScheduled)
        {
            m_tJobHandle.Complete();
            m_bScheduled = false;
        }

        m_grid.Dispose();

        if (m_transformAccessArray.isCreated)
            m_transformAccessArray.Dispose();
        DisposeIfCreated(m_arrRefreshedCenter);

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
            m_listRefreshSlotByColliderId.Add(-1);
        }
    }

    public void RegisterCollider(LegacyBruteForceColliderFinal _refCollider)
    {
        ResizeCapacity(_refCollider.ID);
        m_listColliderByID[_refCollider.ID] = _refCollider;
    }

    public void Activate(LegacyBruteForceColliderFinal _refCollider)
    {
        ResizeCapacity(_refCollider.ID);
        m_listColliderByID[_refCollider.ID] = _refCollider;

        if (m_listIndexInLayerList[_refCollider.ID] >= 0)
            return;

        List<LegacyBruteForceColliderFinal> listLayer = m_arrCollider[_refCollider.Layer];
        m_listIndexInLayerList[_refCollider.ID] = listLayer.Count;
        listLayer.Add(_refCollider);

        // Circle(동적)만 TransformAccessArray에 등록 - Box(정적)는 절대 등록 안 함
        if (_refCollider.Shape == eLegacyColliderShapeFinal.Circle)
        {
            m_listRefreshSlotByColliderId[_refCollider.ID] = m_transformAccessArray.length;
            m_listRefreshColliderIdBySlot.Add(_refCollider.ID);
            m_transformAccessArray.Add(_refCollider.transform);
        }
    }

    public void UnActivate(LegacyBruteForceColliderFinal _refCollider)
    {
        m_listPendingDelete.Add(_refCollider);
    }

    private void DeleteCollider(LegacyBruteForceColliderFinal _refCollider)
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

        List<LegacyBruteForceColliderFinal> listLayer = m_arrCollider[_refCollider.Layer];
        int iLastIndex = listLayer.Count - 1;

        LegacyBruteForceColliderFinal refMoved = listLayer[iLastIndex];
        listLayer[iMyIndex] = refMoved;
        listLayer.RemoveAt(iLastIndex);
        m_listIndexInLayerList[refMoved.ID] = iMyIndex;
        m_listIndexInLayerList[iId] = -1;

        // TransformAccessArray 스왑백 해제 (Circle이었을 때만)
        int iSlot = m_listRefreshSlotByColliderId[iId];
        if (iSlot >= 0)
        {
            int iLastSlot = m_transformAccessArray.length - 1;
            m_transformAccessArray.RemoveAtSwapBack(iSlot);

            int iMovedColliderId = m_listRefreshColliderIdBySlot[iLastSlot];
            m_listRefreshColliderIdBySlot[iSlot] = iMovedColliderId;
            m_listRefreshColliderIdBySlot.RemoveAt(iLastSlot);
            m_listRefreshSlotByColliderId[iMovedColliderId] = iSlot;
            m_listRefreshSlotByColliderId[iId] = -1;
        }
    }

    private void LateUpdate()
    {
        using (s_tMarkerLateUpdate.Auto())
        {
            if (m_bScheduled == false)
                return;

            using (s_tMarkerComplete.Auto())
            {
                m_tJobHandle.Complete();
            }
            m_bScheduled = false;

            DrainResults();
        }
    }

    // 스케줄러([DefaultExecutionOrder(-1000)])가 자기 Update()에서 이걸 호출한다 -
    // 삭제 정리 -> Circle Transform 갱신 -> 그리드 재구성 -> Job Schedule까지 전부 여기서
    public void DoScheduleWork()
    {
        using (s_tMarkerSchedulerUpdate.Auto())
        {
            ProcessPendingDeletes();
            RefreshCircleCenters();
            BuildGrid();
            ScheduleGridJob();
        }
    }

    private void ProcessPendingDeletes()
    {
        for (int i = 0; i < m_listPendingDelete.Count; ++i)
        {
            LegacyBruteForceColliderFinal refCollider = m_listPendingDelete[i];
            if (refCollider.gameObject.activeInHierarchy == false)
                DeleteCollider(refCollider);
        }
        m_listPendingDelete.Clear();
    }

    // Circle(총알/몬스터) 위치를 TransformAccessArray+Job으로 병렬 갱신하고 즉시
    // Complete해서 결과를 CachedCenter에 되돌려 쓴다 - 이 Schedule+Complete 자체는
    // 이번 프레임 그리드 구성 전에 최신 위치가 필요해서 동기적이지만, 예전처럼 메인
    // 스레드가 3000개 넘는 transform.position을 하나씩 순차로 읽던 것과 달리 이제
    // 워커 스레드 병렬 접근이라 훨씬 빠르다(Docs/Collider.md §13/§15 실측 근거)
    private void RefreshCircleCenters()
    {
        using (s_tMarkerRefreshCenter.Auto())
        {
            int iLength = m_transformAccessArray.length;
            if (iLength == 0)
                return;

            EnsureRefreshCapacity(iLength);

            RefreshCenterJob tJob = new RefreshCenterJob { OutCenter = m_arrRefreshedCenter };
            JobHandle tHandle = tJob.Schedule(m_transformAccessArray);
            tHandle.Complete();

            for (int slot = 0; slot < iLength; ++slot)
            {
                int iColliderId = m_listRefreshColliderIdBySlot[slot];
                LegacyBruteForceColliderFinal refCollider = GetColliderByID(iColliderId);
                refCollider?.ApplyCachedCenter(m_arrRefreshedCenter[slot]);
            }
        }
    }

    private void BuildGrid()
    {
        using (s_tMarkerBuildGrid.Auto())
        {
            m_listAllActive.Clear();
            for (int iLayer = 0; iLayer < LAYER_COUNT; ++iLayer)
            {
                List<LegacyBruteForceColliderFinal> listLayer = m_arrCollider[iLayer];
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
                LegacyBruteForceColliderFinal refCollider = m_listAllActive[i];
                Vector3 vCenter = refCollider.CachedCenter;

                m_arrCenter[i] = vCenter;
                m_arrBoundingRadius[i] = refCollider.BoundingRadius;
                m_arrColliderId[i] = refCollider.ID;
                m_arrColliderType[i] = (int)refCollider.Shape;
                m_arrLayerOf[i] = refCollider.Layer;
                m_arrHasPair[i] = m_listOther[refCollider.ID].Count > 0;

                if (refCollider.Shape == eLegacyColliderShapeFinal.Box)
                {
                    LegacyBruteForceBoxColliderFinal refBox = (LegacyBruteForceBoxColliderFinal)refCollider;
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

    private void ScheduleGridJob()
    {
        using (s_tMarkerSchedule.Auto())
        {
            if (m_grid.IsBuilt == false || m_iCount == 0)
                return;

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

            // 워커 스레드가 지금 바로 집어가게 한다 - 이번 프레임 나머지 Update +
            // LateUpdate 전체 동안 겹쳐 돌 시간을 벌기 위함(챕터 4의 핵심)
            JobHandle.ScheduleBatchedJobs();
        }
    }

    private void DrainResults()
    {
        using (s_tMarkerDrain.Auto())
        {
            while (m_queResult.TryDequeue(out tPairResult tResult))
            {
                LegacyBruteForceColliderFinal refA = GetColliderByID(tResult.IdA);
                LegacyBruteForceColliderFinal refB = GetColliderByID(tResult.IdB);

                if (refA == null || refB == null)
                    continue;

                CheckPair(refA, refB, tResult.Overlap);
            }
        }
    }

    private LegacyBruteForceColliderFinal GetColliderByID(int _iId)
    {
        if (_iId < 0 || _iId >= m_listColliderByID.Count)
            return null;

        return m_listColliderByID[_iId];
    }

    private void CheckPair(LegacyBruteForceColliderFinal _refA, LegacyBruteForceColliderFinal _refB, bool _bOverlapping)
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

    // ---- 용량 관리 ----

    private void EnsureRefreshCapacity(int _iCount)
    {
        if (m_arrRefreshedCenter.IsCreated && m_arrRefreshedCenter.Length >= _iCount)
            return;

        int iNewCapacity = m_arrRefreshedCenter.IsCreated ? m_arrRefreshedCenter.Length : INITIAL_SOA_CAPACITY;
        while (iNewCapacity < _iCount)
            iNewCapacity <<= 1;

        DisposeIfCreated(m_arrRefreshedCenter);
        m_arrRefreshedCenter = new NativeArray<Vector3>(iNewCapacity, Allocator.Persistent);
    }

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

    // ---- 순수 판정 함수 (Job이 Burst로 컴파일해 그대로 호출) ----

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

    // ---- Job: Circle Transform 병렬 갱신 ----

    [BurstCompile]
    private struct RefreshCenterJob : IJobParallelForTransform
    {
        public NativeArray<Vector3> OutCenter;

        public void Execute(int index, TransformAccess transform)
        {
            OutCenter[index] = transform.position;
        }
    }

    // ---- Job: 그리드 이웃 조회 + 도형별 판정 (챕터 3과 동일 설계) ----

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

            LegacyColliderGridFinal.ComputeCellCoord(vMyCenter, GridOrigin, CellSize, CountX, CountY, CountZ,
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
                        int iCell = LegacyColliderGridFinal.FlattenIndex(ix, iy, iz, CountX, CountY);
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
                                bOverlap = LegacyBruteForceColliderManagerFinal.IsCircleCircleOverlap(vMyCenter, fMyRadius, vOtherCenter, fOtherRadius);
                            else if (iMyType == COLLIDER_TYPE_BOX && iOtherType == COLLIDER_TYPE_BOX)
                                bOverlap = false;
                            else
                            {
                                float fBoundSum = fMyRadius + fOtherRadius;
                                Vector3 vDelta = vOtherCenter - vMyCenter;
                                bOverlap = false;

                                if (vDelta.sqrMagnitude <= fBoundSum * fBoundSum)
                                {
                                    bOverlap = iMyType == COLLIDER_TYPE_BOX
                                        ? LegacyBruteForceColliderManagerFinal.IsCircleBoxOverlap(
                                            vOtherCenter, fOtherRadius, vMyCenter, AxisX[index], AxisY[index], AxisZ[index], HalfExtent[index])
                                        : LegacyBruteForceColliderManagerFinal.IsCircleBoxOverlap(
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
