using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;

/*///////////////////////////////////////////
        LegacyBruteForceColliderManagerJob
목적 : 커밋 daef9e3(Box 브로드페이즈를 Burst IJobParallelFor로 교체) 시점을 재현하는
       벤치마크 전용 매니저 - GridTestScene0(LegacyBruteForceColliderManager, 8edea44)의
       코드는 건드리지 않고 완전히 새 타입으로 분리했다("버전2 새 파일" 지시).

       구조: 수집 -> Box Job Schedule -> (Job이 워커에서 도는 동안) Circle-Circle
       브루트포스를 메인스레드에서 실행 -> Complete -> 드레인해서 Enter/Stay/Exit 발화.
       이 벤치마크는 Box(Obstacle) 레이어가 정확히 하나뿐이라, 프로덕션
       ColliderManager처럼 레이어별 그리드 배열/대기 목록을 두지 않고 그리드 하나 +
       Box/Other(총알) SoA 한 벌로 단순화했다 - 기법(Job 스케줄 후 메인스레드와 겹쳐
       돌리기)은 원본과 동일, 다중 Box 레이어 일반화만 생략.

       [DefaultExecutionOrder(1000)]는 GridTestScene0/원본과 동일하게 유지 - 이
       벤치마크의 LegacyBruteForceMover가 Update()에서 직접 이동하므로 판정은 그
       이후(LateUpdate)에 돌아야 정확하다.
 *///////////////////////////////////////////
[DefaultExecutionOrder(1000)]
public sealed class LegacyBruteForceColliderManagerJob : MonoBehaviour
{
    private const int JOB_BATCH_SIZE = 64;
    private const int INITIAL_SOA_CAPACITY = 64;

    public static LegacyBruteForceColliderManagerJob Instance { get; private set; }

    private readonly LayerMask[] m_arrLayerCollisionMatrix = new LayerMask[32];
    private int m_iBulletLayer = -1;
    private int m_iObstacleLayer = -1;
    private int m_iMonsterLayer = -1;

    private List<LegacyBruteForceColliderJob>[] m_arrCollider;
    private List<LegacyBruteForceColliderJob> m_listPendingDelete;
    private List<int> m_listIndexInLayerList;
    private List<LegacyBruteForceColliderJob> m_listColliderByID; // Job 결과(ID)를 실제 참조로 되돌리는 용도
    private List<HashSet<int>> m_listOther;
    private Dictionary<long, tColliderPair> m_hashPairInfo;

    private readonly LegacyBoxColliderGridJob m_gridBox = new LegacyBoxColliderGridJob();
    private bool m_bBoxPairFoundThisFrame;

    // Box(Obstacle) SoA
    private NativeArray<Vector3> m_arrBoxCenter;
    private NativeArray<Vector3> m_arrBoxAxisX;
    private NativeArray<Vector3> m_arrBoxAxisY;
    private NativeArray<Vector3> m_arrBoxAxisZ;
    private NativeArray<Vector3> m_arrBoxHalfExtent;
    private NativeArray<float> m_arrBoxBoundingRadius;
    private NativeArray<int> m_arrBoxColliderId;
    private int m_iBoxCount;

    // Other(총알) SoA - Box 그리드를 조회하는 쪽
    private NativeArray<Vector3> m_arrOtherCenter;
    private NativeArray<float> m_arrOtherRadius;
    private NativeArray<int> m_arrOtherColliderId;
    private NativeArray<bool> m_arrOtherHasPair;
    private int m_iOtherCount;

    private NativeQueue<tPairResult> m_queResult;
    private JobHandle m_tJobHandle;
    private bool m_bScheduled;

    private static readonly ProfilerMarker s_tMarkerLateUpdate = new ProfilerMarker("LegacyBruteForceJob.LateUpdate");
    private static readonly ProfilerMarker s_tMarkerRefreshCenter = new ProfilerMarker("LegacyBruteForceJob.PreLoadCenter");
    private static readonly ProfilerMarker s_tMarkerBoxGridUpdate = new ProfilerMarker("LegacyBruteForceJob.UpdateBoxGrid");
    private static readonly ProfilerMarker s_tMarkerGridSchedule = new ProfilerMarker("LegacyBruteForceJob.GridJobSchedule");
    private static readonly ProfilerMarker s_tMarkerGridComplete = new ProfilerMarker("LegacyBruteForceJob.GridJobComplete");
    private static readonly ProfilerMarker s_tMarkerGridDrain = new ProfilerMarker("LegacyBruteForceJob.GridJobDrain");
    private static readonly ProfilerMarker s_tMarkerSameLayer = new ProfilerMarker("LegacyBruteForceJob.CheckSameLayer");
    private static readonly ProfilerMarker s_tMarkerCrossLayer = new ProfilerMarker("LegacyBruteForceJob.CheckCrossLayer");
    private static readonly ProfilerMarker s_tMarkerStay = new ProfilerMarker("LegacyBruteForceJob.OnStay");

    private struct tColliderPair
    {
        public LegacyBruteForceColliderJob ColliderA;
        public LegacyBruteForceColliderJob ColliderB;
    }

    private struct tPairResult
    {
        public int CircleId;
        public int BoxId;
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

        m_arrCollider = new List<LegacyBruteForceColliderJob>[32];
        for (int i = 0; i < 32; ++i)
            m_arrCollider[i] = new List<LegacyBruteForceColliderJob>();

        m_listPendingDelete = new List<LegacyBruteForceColliderJob>();
        m_listIndexInLayerList = new List<int>();
        m_listColliderByID = new List<LegacyBruteForceColliderJob>();
        m_listOther = new List<HashSet<int>>();
        m_hashPairInfo = new Dictionary<long, tColliderPair>();

        m_queResult = new NativeQueue<tPairResult>(Allocator.Persistent);
    }

    private void OnDestroy()
    {
        if (m_bScheduled)
        {
            m_tJobHandle.Complete();
            m_bScheduled = false;
        }

        m_gridBox.Dispose();

        DisposeIfCreated(m_arrBoxCenter);
        DisposeIfCreated(m_arrBoxAxisX);
        DisposeIfCreated(m_arrBoxAxisY);
        DisposeIfCreated(m_arrBoxAxisZ);
        DisposeIfCreated(m_arrBoxHalfExtent);
        DisposeIfCreated(m_arrBoxBoundingRadius);
        DisposeIfCreated(m_arrBoxColliderId);

        DisposeIfCreated(m_arrOtherCenter);
        DisposeIfCreated(m_arrOtherRadius);
        DisposeIfCreated(m_arrOtherColliderId);
        DisposeIfCreated(m_arrOtherHasPair);

        if (m_queResult.IsCreated)
            m_queResult.Dispose();
    }

    private static void DisposeIfCreated<T>(NativeArray<T> _arr) where T : struct
    {
        if (_arr.IsCreated)
            _arr.Dispose();
    }

    // 스포너가 씬 시작 시 한 번 호출
    public void ConfigureLayerMatrix(int _iBulletLayer, int _iObstacleLayer, int _iMonsterLayer)
    {
        m_iBulletLayer = _iBulletLayer;
        m_iObstacleLayer = _iObstacleLayer;
        m_iMonsterLayer = _iMonsterLayer;
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
            m_listColliderByID.Add(null);
        }
    }

    public void RegisterCollider(LegacyBruteForceColliderJob _refCollider)
    {
        ResizeCapacity(_refCollider.ID);
        m_listColliderByID[_refCollider.ID] = _refCollider;
    }

    public void Activate(LegacyBruteForceColliderJob _refCollider)
    {
        ResizeCapacity(_refCollider.ID);
        m_listColliderByID[_refCollider.ID] = _refCollider;

        if (m_listIndexInLayerList[_refCollider.ID] >= 0)
            return;

        List<LegacyBruteForceColliderJob> listLayer = m_arrCollider[_refCollider.Layer];
        m_listIndexInLayerList[_refCollider.ID] = listLayer.Count;
        listLayer.Add(_refCollider);
    }

    public void UnActivate(LegacyBruteForceColliderJob _refCollider)
    {
        m_listPendingDelete.Add(_refCollider);
    }

    private void DeleteCollider(LegacyBruteForceColliderJob _refCollider)
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

        List<LegacyBruteForceColliderJob> listLayer = m_arrCollider[_refCollider.Layer];
        int iLastIndex = listLayer.Count - 1;

        LegacyBruteForceColliderJob refMoved = listLayer[iLastIndex];
        listLayer[iMyIndex] = refMoved;
        listLayer.RemoveAt(iLastIndex);
        m_listIndexInLayerList[refMoved.ID] = iMyIndex;
        m_listIndexInLayerList[iId] = -1;
    }

    private void Update()
    {
        for (int i = 0; i < m_listPendingDelete.Count; ++i)
        {
            LegacyBruteForceColliderJob refCollider = m_listPendingDelete[i];
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

    // 수집 -> Box Job Schedule -> (Job이 도는 동안) Circle-Circle 브루트포스 -> Complete -> 드레인
    private void CheckOverlaps()
    {
        PreLoadCenter();
        UpdateBoxGrid();

        m_bBoxPairFoundThisFrame = ScheduleBoxJob();

        RunNonBoxPairs();

        if (m_bBoxPairFoundThisFrame)
            CompleteAndDrainBoxJob();
    }

    private void PreLoadCenter()
    {
        using (s_tMarkerRefreshCenter.Auto())
        {
            for (int iLayer = 0; iLayer < 32; ++iLayer)
            {
                List<LegacyBruteForceColliderJob> listLayer = m_arrCollider[iLayer];
                for (int i = 0; i < listLayer.Count; ++i)
                    listLayer[i].RefreshCenter();
            }
        }
    }

    // Box(Obstacle) 레이어의 그리드 + SoA를 매 프레임 다시 채운다. 그리드 자체는
    // Build 시점에 한 번만 지어지고("정적 분할"), 이후엔 멤버십만 매번 다시 계산한다
    private void UpdateBoxGrid()
    {
        using (s_tMarkerBoxGridUpdate.Auto())
        {
            m_iBoxCount = 0;
            if (m_iObstacleLayer < 0)
                return;

            List<LegacyBruteForceColliderJob> listBox = m_arrCollider[m_iObstacleLayer];
            if (listBox.Count == 0)
                return;

            if (m_gridBox.IsBuilt == false)
                m_gridBox.Build(listBox);
            if (m_gridBox.IsBuilt == false)
                return;

            EnsureBoxCapacity(listBox.Count);
            m_gridBox.BeginRebuild(listBox.Count);

            for (int i = 0; i < listBox.Count; ++i)
            {
                LegacyBruteForceBoxColliderJob refBox = (LegacyBruteForceBoxColliderJob)listBox[i];
                Vector3 vCenter = refBox.CachedCenter;

                m_arrBoxCenter[i] = vCenter;
                m_arrBoxAxisX[i] = refBox.AxisX;
                m_arrBoxAxisY[i] = refBox.AxisY;
                m_arrBoxAxisZ[i] = refBox.AxisZ;
                m_arrBoxHalfExtent[i] = refBox.HalfExtent;
                m_arrBoxBoundingRadius[i] = refBox.BoundingRadius;
                m_arrBoxColliderId[i] = refBox.ID;

                m_gridBox.AddItem(i, vCenter);
            }

            m_gridBox.EndRebuild();
            m_iBoxCount = listBox.Count;
        }
    }

    // 총알(Other) SoA를 채우고 Box 그리드 조회 Job을 스케줄한다. 반환값: 스케줄했으면 true
    private bool ScheduleBoxJob()
    {
        using (s_tMarkerGridSchedule.Auto())
        {
            if (m_gridBox.IsBuilt == false || m_iBoxCount == 0 || m_iBulletLayer < 0)
                return false;

            List<LegacyBruteForceColliderJob> listOther = m_arrCollider[m_iBulletLayer];
            if (listOther.Count == 0)
                return false;

            EnsureOtherCapacity(listOther.Count);

            m_iOtherCount = 0;
            for (int i = 0; i < listOther.Count; ++i)
            {
                LegacyBruteForceCircleColliderJob refCircle = listOther[i] as LegacyBruteForceCircleColliderJob;
                if (refCircle == null)
                    continue;

                m_arrOtherCenter[m_iOtherCount] = refCircle.CachedCenter;
                m_arrOtherRadius[m_iOtherCount] = refCircle.Radius;
                m_arrOtherColliderId[m_iOtherCount] = refCircle.ID;
                m_arrOtherHasPair[m_iOtherCount] = m_listOther[refCircle.ID].Count > 0;
                ++m_iOtherCount;
            }

            if (m_iOtherCount == 0)
                return false;

            CircleBoxOverlapJob tJob = new CircleBoxOverlapJob
            {
                CellStart = m_gridBox.CellStart,
                CellCount = m_gridBox.CellCount,
                CellItems = m_gridBox.CellItems,
                GridOrigin = m_gridBox.Origin,
                CellSize = m_gridBox.CellSize,
                CountX = m_gridBox.CountX,
                CountY = m_gridBox.CountY,
                CountZ = m_gridBox.CountZ,

                BoxCenter = m_arrBoxCenter,
                BoxAxisX = m_arrBoxAxisX,
                BoxAxisY = m_arrBoxAxisY,
                BoxAxisZ = m_arrBoxAxisZ,
                BoxHalfExtent = m_arrBoxHalfExtent,
                BoxBoundingRadius = m_arrBoxBoundingRadius,
                BoxColliderId = m_arrBoxColliderId,

                OtherCenter = m_arrOtherCenter,
                OtherRadius = m_arrOtherRadius,
                OtherColliderId = m_arrOtherColliderId,
                OtherHasPair = m_arrOtherHasPair,

                Output = m_queResult.AsParallelWriter()
            };

            m_tJobHandle = tJob.Schedule(m_iOtherCount, JOB_BATCH_SIZE);
            m_bScheduled = true;

            // 워커 스레드가 Complete까지 기다리지 않고 지금 바로 집어가게 한다 - 이게 없으면
            // "메인스레드가 다른 일 하는 동안 겹쳐 돈다"는 전제가 깨진다
            JobHandle.ScheduleBatchedJobs();
            return true;
        }
    }

    // Job이 워커에서 도는 동안 메인스레드가 처리하는 몫 - 이 벤치마크에선 Circle-Circle
    // (총알↔몬스터) 브루트포스와, 혹시 모를 same-layer 쌍이 여기 해당한다
    private void RunNonBoxPairs()
    {
        for (int iLayerA = 0; iLayerA < 32; ++iLayerA)
        {
            List<LegacyBruteForceColliderJob> listA = m_arrCollider[iLayerA];
            if (listA.Count == 0)
                continue;

            for (int iLayerB = iLayerA; iLayerB < 32; ++iLayerB)
            {
                if (!IsLayerCollider(iLayerA, iLayerB))
                    continue;

                List<LegacyBruteForceColliderJob> listB = m_arrCollider[iLayerB];
                if (listB.Count == 0)
                    continue;

                if (iLayerA == iLayerB)
                {
                    CheckSameLayer(listA);
                    continue;
                }

                bool bBoxA = listA[0].Shape == eLegacyColliderShapeJob.Box;
                bool bBoxB = listB[0].Shape == eLegacyColliderShapeJob.Box;

                // Box가 낀 쌍은 이미 Job으로 처리 중이니 여기서 건드리지 않는다.
                // Box-Box는 판정 함수가 항상 false라 순회 자체를 생략해도 결과가 같다
                if (bBoxA || bBoxB)
                    continue;

                CheckCrossLayer(listA, listB);
            }
        }
    }

    // Job 결과를 실제 콜라이더로 되돌려 Enter/Stay/Exit을 발화시키는 유일한 지점.
    // CheckPair 인자 순서는 메인 루프(A<=B)와 똑같이 낮은 레이어가 A가 되도록 맞춘다
    private void CompleteAndDrainBoxJob()
    {
        if (m_bScheduled == false)
            return;

        using (s_tMarkerGridComplete.Auto())
        {
            m_tJobHandle.Complete();
        }
        m_bScheduled = false;

        using (s_tMarkerGridDrain.Auto())
        {
            while (m_queResult.TryDequeue(out tPairResult tResult))
            {
                LegacyBruteForceColliderJob refBox = GetColliderByID(tResult.BoxId);
                LegacyBruteForceColliderJob refCircle = GetColliderByID(tResult.CircleId);

                if (refBox == null || refCircle == null)
                    continue;

                if (refBox.Layer < refCircle.Layer)
                    CheckPair(refBox, refCircle, tResult.Overlap);
                else
                    CheckPair(refCircle, refBox, tResult.Overlap);
            }
        }
    }

    private LegacyBruteForceColliderJob GetColliderByID(int _iId)
    {
        if (_iId < 0 || _iId >= m_listColliderByID.Count)
            return null;

        return m_listColliderByID[_iId];
    }

    private void CheckSameLayer(List<LegacyBruteForceColliderJob> _listCollider)
    {
        using (s_tMarkerSameLayer.Auto())
        {
            for (int a = 0; a < _listCollider.Count; ++a)
            {
                LegacyBruteForceColliderJob refI = _listCollider[a];
                for (int b = a + 1; b < _listCollider.Count; ++b)
                    CheckPair(refI, _listCollider[b]);
            }
        }
    }

    private void CheckCrossLayer(List<LegacyBruteForceColliderJob> _listA, List<LegacyBruteForceColliderJob> _listB)
    {
        using (s_tMarkerCrossLayer.Auto())
        {
            for (int a = 0; a < _listA.Count; ++a)
            {
                LegacyBruteForceColliderJob refA = _listA[a];
                for (int b = 0; b < _listB.Count; ++b)
                    CheckPair(refA, _listB[b]);
            }
        }
    }

    private void CheckPair(LegacyBruteForceColliderJob _refA, LegacyBruteForceColliderJob _refB)
    {
        CheckPair(_refA, _refB, IsOverlapping(_refA, _refB));
    }

    // 겹침 여부를 이미 아는 호출부(Job 결과 드레인)를 위해 판정 결과를 인자로 받는 오버로드 -
    // 메인스레드에서 같은 수학을 두 번 돌리지 않기 위함
    private void CheckPair(LegacyBruteForceColliderJob _refA, LegacyBruteForceColliderJob _refB, bool _bOverlapping)
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

    // ---- 도형별 겹침 판정 (메인스레드 전용 경로 - Circle-Circle만 여기로 옴) ----

    private static bool IsOverlapping(LegacyBruteForceColliderJob _refA, LegacyBruteForceColliderJob _refB)
    {
        // 이 벤치마크에서 메인스레드로 오는 크로스 레이어 쌍은 항상 Circle-Circle뿐이다
        // (Box가 낀 쌍은 전부 Job이 처리) - same-layer 쌍도 지금 매트릭스엔 없음
        LegacyBruteForceCircleColliderJob refCircleA = (LegacyBruteForceCircleColliderJob)_refA;
        LegacyBruteForceCircleColliderJob refCircleB = (LegacyBruteForceCircleColliderJob)_refB;

        Vector3 vDelta = refCircleB.CachedCenter - refCircleA.CachedCenter;
        float fDistSq = vDelta.sqrMagnitude;
        float fRadiusSum = refCircleA.Radius + refCircleB.Radius;
        return fDistSq <= fRadiusSum * fRadiusSum;
    }

    // 원(구)-OBB 판정 순수 함수 - CircleBoxOverlapJob이 Burst로 컴파일해 그대로 호출한다
    // (관리형 참조 없이 값만 받으므로 안전)
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

    // ---- SoA 용량 관리 (스크래치 버퍼라 매 프레임 통째로 덮어씀 - 모자라면 더블링) ----

    private void EnsureBoxCapacity(int _iCount)
    {
        if (m_arrBoxCenter.IsCreated && m_arrBoxCenter.Length >= _iCount)
            return;

        int iNewCapacity = m_arrBoxCenter.IsCreated ? m_arrBoxCenter.Length : INITIAL_SOA_CAPACITY;
        while (iNewCapacity < _iCount)
            iNewCapacity <<= 1;

        DisposeIfCreated(m_arrBoxCenter);
        DisposeIfCreated(m_arrBoxAxisX);
        DisposeIfCreated(m_arrBoxAxisY);
        DisposeIfCreated(m_arrBoxAxisZ);
        DisposeIfCreated(m_arrBoxHalfExtent);
        DisposeIfCreated(m_arrBoxBoundingRadius);
        DisposeIfCreated(m_arrBoxColliderId);

        m_arrBoxCenter = new NativeArray<Vector3>(iNewCapacity, Allocator.Persistent);
        m_arrBoxAxisX = new NativeArray<Vector3>(iNewCapacity, Allocator.Persistent);
        m_arrBoxAxisY = new NativeArray<Vector3>(iNewCapacity, Allocator.Persistent);
        m_arrBoxAxisZ = new NativeArray<Vector3>(iNewCapacity, Allocator.Persistent);
        m_arrBoxHalfExtent = new NativeArray<Vector3>(iNewCapacity, Allocator.Persistent);
        m_arrBoxBoundingRadius = new NativeArray<float>(iNewCapacity, Allocator.Persistent);
        m_arrBoxColliderId = new NativeArray<int>(iNewCapacity, Allocator.Persistent);
    }

    private void EnsureOtherCapacity(int _iCount)
    {
        if (m_arrOtherCenter.IsCreated && m_arrOtherCenter.Length >= _iCount)
            return;

        int iNewCapacity = m_arrOtherCenter.IsCreated ? m_arrOtherCenter.Length : INITIAL_SOA_CAPACITY;
        while (iNewCapacity < _iCount)
            iNewCapacity <<= 1;

        DisposeIfCreated(m_arrOtherCenter);
        DisposeIfCreated(m_arrOtherRadius);
        DisposeIfCreated(m_arrOtherColliderId);
        DisposeIfCreated(m_arrOtherHasPair);

        m_arrOtherCenter = new NativeArray<Vector3>(iNewCapacity, Allocator.Persistent);
        m_arrOtherRadius = new NativeArray<float>(iNewCapacity, Allocator.Persistent);
        m_arrOtherColliderId = new NativeArray<int>(iNewCapacity, Allocator.Persistent);
        m_arrOtherHasPair = new NativeArray<bool>(iNewCapacity, Allocator.Persistent);
    }

    // ---- Job ----

    // NativeArray만 참조하는 순수 struct - 총알(Other) 하나당 자기 셀 기준 이웃 27칸의
    // Box 후보만 검사한다(LegacyBoxColliderGridJob과 동일 근거)
    [BurstCompile]
    private struct CircleBoxOverlapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> CellStart;
        [ReadOnly] public NativeArray<int> CellCount;
        [ReadOnly] public NativeArray<int> CellItems;

        public Vector3 GridOrigin;
        public float CellSize;
        public int CountX;
        public int CountY;
        public int CountZ;

        [ReadOnly] public NativeArray<Vector3> BoxCenter;
        [ReadOnly] public NativeArray<Vector3> BoxAxisX;
        [ReadOnly] public NativeArray<Vector3> BoxAxisY;
        [ReadOnly] public NativeArray<Vector3> BoxAxisZ;
        [ReadOnly] public NativeArray<Vector3> BoxHalfExtent;
        [ReadOnly] public NativeArray<float> BoxBoundingRadius;
        [ReadOnly] public NativeArray<int> BoxColliderId;

        [ReadOnly] public NativeArray<Vector3> OtherCenter;
        [ReadOnly] public NativeArray<float> OtherRadius;
        [ReadOnly] public NativeArray<int> OtherColliderId;
        [ReadOnly] public NativeArray<bool> OtherHasPair;

        public NativeQueue<tPairResult>.ParallelWriter Output;

        public void Execute(int index)
        {
            Vector3 vCenter = OtherCenter[index];
            float fRadius = OtherRadius[index];
            int iCircleId = OtherColliderId[index];
            bool bHasPair = OtherHasPair[index];

            LegacyBoxColliderGridJob.ComputeCellCoord(vCenter, GridOrigin, CellSize, CountX, CountY, CountZ,
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
                        int iCell = LegacyBoxColliderGridJob.FlattenIndex(ix, iy, iz, CountX, CountY);
                        int iStart = CellStart[iCell];
                        int iEnd = iStart + CellCount[iCell];

                        for (int k = iStart; k < iEnd; ++k)
                        {
                            int iBox = CellItems[k];
                            Vector3 vBoxCenter = BoxCenter[iBox];

                            // 구-구 선판정으로 먼저 거른다 - 통과 못 하면 진짜 OBB 판정 없이도 100% 안 겹침
                            float fBoundSum = fRadius + BoxBoundingRadius[iBox];
                            Vector3 vDelta = vCenter - vBoxCenter;

                            bool bOverlap = false;
                            if (vDelta.sqrMagnitude <= fBoundSum * fBoundSum)
                            {
                                bOverlap = LegacyBruteForceColliderManagerJob.IsCircleBoxOverlap(
                                    vCenter, fRadius,
                                    vBoxCenter, BoxAxisX[iBox], BoxAxisY[iBox], BoxAxisZ[iBox], BoxHalfExtent[iBox]);
                            }

                            // 안 겹친 결과는 "저번 프레임까지 겹쳐있던 쌍"의 Exit 판정에만 필요하다
                            if (bOverlap || bHasPair)
                            {
                                Output.Enqueue(new tPairResult
                                {
                                    CircleId = iCircleId,
                                    BoxId = BoxColliderId[iBox],
                                    Overlap = bOverlap
                                });
                            }
                        }
                    }
        }
    }
}
