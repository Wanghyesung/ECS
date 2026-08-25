using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;

/*///////////////////////////////////////////
              ColliderManager
목적 : BaseCollider(Circle/Obb)들을 PhysX 없이 자체적으로 충돌 판정한다.

       레이어 무관 활성 콜라이더 전부를 SoA 하나 + BoxColliderGrid 하나에 담는다 - 그리드
       소유/조회 구분 없이, 그리드에 들어간 콜라이더 자신이 곧 조회 주체다(이웃 27칸 탐색,
       후보 index가 자기 이하면 스킵). GridOverlapJob.Execute 하나가 이웃 탐색 + 레이어
       매트릭스 필터 + 도형 분기(Circle-Circle/Circle-Box/Box-Box)까지 전부 처리한다.

       Job Schedule과 Complete를 서로 다른 실행 순서로 쪼갠다 - ScheduleFrame()은
       ColliderManagerScheduler([DefaultExecutionOrder(-1000)], 이 프레임에서 가장 먼저)가
       부르고, Complete+드레인은 이 클래스의 LateUpdate([DefaultExecutionOrder(1000)], 가장
       나중)가 한다. 한 클래스는 메서드별로 다른 실행 순서를 못 가지므로 Schedule 쪽만 별도
       컴포넌트로 뗀 것 - 이렇게 하면 충돌 Job이 이번 프레임 나머지 Update 전체 + LateUpdate
       전체 동안 워커 스레드에서 겹쳐 돈다. 한 프레임의 모든 Enter/Stay/Exit이 동일한 위치
       스냅샷(정확히 한 프레임 전) 기준으로 계산되므로 콜백 순서에 판정이 갈리는 문제도 없다.
       대가는 판정이 실제 최신 위치보다 한 프레임(~16ms@60fps) 늦음.

       위치/축(transform.position/rotation) 갱신은 ColliderCenterRefresher가 전담한다 -
       "이동 추적"과 "충돌 판정"을 분리한 것.
 *///////////////////////////////////////////

// LateUpdate가 이 프레임에서 가장 늦게 돌수록 ColliderManagerScheduler가 미리 Schedule한
// Job이 겹쳐 도는 시간이 길어진다(클래스 헤더 참고) - 그래서 기본 순서보다 뒤로 고정
[DefaultExecutionOrder(1000)]
public class ColliderManager : MonoBehaviour
{
    public static ColliderManager m_Instance = null;

    private const int LAYER_COUNT = 32;
    private const int JOB_BATCH_SIZE = 64;
    private const int INITIAL_CAPACITY = 1024;
    private const int COLLIDER_TYPE_BOX = (int)eColliderShape.Box;

    // 레이어 i가 충돌할 레이어 마스크. 한쪽만 등록해도 양방향 인식됨(IsLayerCollider 참고)
    [SerializeField] private LayerMask[] m_arrLayerCollisionMatrix = new LayerMask[LAYER_COUNT];

    // Box(Obstacle) 컬링 기준점 - 비워두면 컬링 없이 전부 대상(안전한 기본값). Circle엔 미적용
    [SerializeField] private Transform m_refPlayer;
    [SerializeField] private float m_fMaxBulletRange = 1000f;

    // 레이어(0~31) -> 활성 콜라이더 목록. 레이어 하나엔 한 가지 도형만 들어간다는 게 전제
    private List<BaseCollider>[] m_arrCollider;

    // UnActivate 예약 - 다음 프레임 Update() 맨 앞에서 일괄 스왑백 제거(판정 순회 중 스왑백하면
    // 방금 옮겨온 콜라이더가 이번 프레임에서 스킵될 수 있어서 즉시 제거하지 않음)
    private List<BaseCollider> m_listPendingDelete;

    // ID -> 레이어 리스트에서 몇 번째 자리인지 (스왑백 O(1) 제거용)
    private List<int> m_listIndexInLayerList;
    // ID -> BaseCollider 본체. Job 결과(ID 쌍)를 객체로 되돌릴 때 사용
    private List<BaseCollider> m_listColliderByID;
    // ID -> 지금 겹쳐있다고 기록된 상대 ID들. Exit 조회 스킵/DeleteCollider 정리에 사용
    private List<HashSet<int>> m_listOther;

    // 위치/축 갱신(이동 추적) 전담 - Activate/DeleteCollider와 짝 맞춰 Register/Unregister 호출
    private ColliderCenterRefresher m_refCenterRefresher;

    // 쌍(A,B) 존재 = 지금 겹치는 중. CheckPair가 이 하나로 Enter/Stay/Exit 전부 처리
    private Dictionary<long, tColliderPair> m_hashPairInfo;

    private static readonly ProfilerMarker s_tMarkerStay = new ProfilerMarker("ColliderManager.OnStay");
    private static readonly ProfilerMarker s_tMarkerRefreshCenter = new ProfilerMarker("ColliderManager.PreLoadCenter");
    private static readonly ProfilerMarker s_tMarkerGather = new ProfilerMarker("ColliderManager.Gather");
    private static readonly ProfilerMarker s_tMarkerGridBuild = new ProfilerMarker("ColliderManager.GridBuild");
    private static readonly ProfilerMarker s_tMarkerGridSchedule = new ProfilerMarker("ColliderManager.GridJobSchedule");
    private static readonly ProfilerMarker s_tMarkerGridComplete = new ProfilerMarker("ColliderManager.GridJobComplete");
    private static readonly ProfilerMarker s_tMarkerGridDrain = new ProfilerMarker("ColliderManager.GridJobDrain");

    // 활성 콜라이더 전부가 들어가는 단일 공간 그리드
    private BoxColliderGrid m_grid;

    // m_grid.Build()용 일회성 스크래치(그리드가 아직 안 지어졌을 때만 채움) - SoA/그리드
    // 자체는 m_arrCollider[layer]를 매 프레임 직접 순회해서 채운다(BuildGrid 참고)
    private List<BaseCollider> m_listActiveCollider;

    // --- Job 입력용 SoA (Allocator.Persistent, 매 프레임 통째로 덮어씀) ---
    // AxisX/Y/Z/HalfExtent는 ColliderType==Box일 때만 채워지고 Circle 항목은 안 읽힘
    private NativeArray<Vector3> m_arrCenter;
    private NativeArray<Vector3> m_arrAxisX;
    private NativeArray<Vector3> m_arrAxisY;
    private NativeArray<Vector3> m_arrAxisZ;
    private NativeArray<Vector3> m_arrHalfExtent;
    private NativeArray<float> m_arrBoundingRadius;
    private NativeArray<int> m_arrColliderId;
    private NativeArray<int> m_arrColliderType;
    private NativeArray<int> m_arrLayer;
    // 안 겹친 결과를 큐에 담을지 결정하는 필터 - 기록 없으면 Exit도 없으므로 안 담아도 결과는 동일
    private NativeArray<bool> m_arrHasPair;
    private int m_iCount;

    // 레이어 매트릭스를 Burst Job이 읽을 수 있는 값 배열로 복사(Awake 시 한 번, 런타임 불변)
    private NativeArray<int> m_arrLayerMatrixValue;

    // Job 결과. NativeQueue라 사전 용량 예약 없이도 최악의 프레임에 결과가 유실되지 않는다
    private NativeQueue<tPairResult> m_queResult;

    private JobHandle m_tJobHandle;
    private bool m_bScheduled;

    private struct tColliderPair
    {
        public BaseCollider ColliderA;
        public BaseCollider ColliderB;
    }

    // Overlap=false 항목은 "후보였는데 안 겹쳤다"는 뜻 - 저번 프레임 겹침의 Exit 판정에 필요
    private struct tPairResult
    {
        public int IdA;
        public int IdB;
        public bool Overlap;
    }

    private void Awake()
    {
        if (m_Instance != null)
        {
            Destroy(this);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(this);

        // ScheduleFrame()을 이 프레임 최대한 일찍 호출해줄 트리거를 자동으로 붙인다 -
        gameObject.AddComponent<ColliderManagerScheduler>();

        m_arrCollider = new List<BaseCollider>[LAYER_COUNT];
        for (int i = 0; i < LAYER_COUNT; ++i)
            m_arrCollider[i] = new List<BaseCollider>();

        m_grid = new BoxColliderGrid();
        m_listActiveCollider = new List<BaseCollider>();
        m_refCenterRefresher = new ColliderCenterRefresher(INITIAL_CAPACITY);

        m_listPendingDelete = new List<BaseCollider>();
        m_listIndexInLayerList = new List<int>();
        m_listColliderByID = new List<BaseCollider>();
        m_listOther = new List<HashSet<int>>();

        m_hashPairInfo = new Dictionary<long, tColliderPair>();

        m_arrLayerMatrixValue = new NativeArray<int>(LAYER_COUNT, Allocator.Persistent);
        for (int i = 0; i < LAYER_COUNT; ++i)
            m_arrLayerMatrixValue[i] = m_arrLayerCollisionMatrix[i].value;

        AllocateSoa(INITIAL_CAPACITY);
        m_queResult = new NativeQueue<tPairResult>(Allocator.Persistent);
    }

    // 진행 중인 Job을 먼저 끝낸 뒤 모든 NativeContainer를 해제한다(워커가 이미 Dispose된
    // 메모리를 만지지 않도록). 중복 인스턴스는 Awake에서 아무것도 할당하지 않았으므로 안전
    private void OnDestroy()
    {
        if (m_bScheduled)
        {
            m_tJobHandle.Complete();
            m_bScheduled = false;
        }

        DisposeSoa();

        if (m_arrLayerMatrixValue.IsCreated)
            m_arrLayerMatrixValue.Dispose();

        if (m_queResult.IsCreated)
            m_queResult.Dispose();

        m_refCenterRefresher?.Dispose();
        m_grid?.Dispose();
    }

    private static long MakePairKey(int _iA, int _iB)
    {
        int iLow = _iA < _iB ? _iA : _iB;
        int iHigh = _iA < _iB ? _iB : _iA;
        return ((long)iLow << 32) | (uint)iHigh;
    }

    // 관리형 참조 없이 값 배열만 받는 raw-parameter 버전 - Burst Job이 그대로 호출 가능,
    // 씬 없이 EditMode 테스트로도 검증 가능
    public static bool IsLayerCollider(NativeArray<int> _arrMatrixValue, int _iLayerA, int _iLayerB)
    {
        bool bAToB = (_arrMatrixValue[_iLayerA] & (1 << _iLayerB)) != 0;
        bool bBToA = (_arrMatrixValue[_iLayerB] & (1 << _iLayerA)) != 0;
        return bAToB || bBToA;
    }

    private void ResizeCapacity(int _iID)
    {
        while (m_listIndexInLayerList.Count <= _iID)
        {
            m_listIndexInLayerList.Add(-1);
            m_listColliderByID.Add(null);
            m_listOther.Add(new HashSet<int>());
        }

        m_refCenterRefresher.ResizeIdCapacity(_iID);
    }

    // BaseCollider.Start()에서 생애주기 중 한 번만 호출. 레이어 리스트엔 아직 안 들어감(Activate가 담당)
    public void RegisterCollider(BaseCollider _refCollider)
    {
        ResizeCapacity(_refCollider.ID);
        m_listColliderByID[_refCollider.ID] = _refCollider;
    }

    // BaseCollider.OnEnable()/Start()에서 호출. 이미 등록돼 있으면 무시(재발사 등 중복 등록 방지)
    public void Activate(BaseCollider _refCollider)
    {
        ResizeCapacity(_refCollider.ID);

        // OnEnable이 Start(=RegisterCollider)보다 먼저 도는 경로가 있어 여기서도 채워둠 -
        // 비어 있으면 Job 결과를 객체로 되돌릴 때 그 쌍이 통째로 무시된다
        m_listColliderByID[_refCollider.ID] = _refCollider;

        if (m_listIndexInLayerList[_refCollider.ID] >= 0)
            return;

        List<BaseCollider> listLayer = m_arrCollider[_refCollider.Layer];
        m_listIndexInLayerList[_refCollider.ID] = listLayer.Count;
        listLayer.Add(_refCollider);

        m_refCenterRefresher.Register(_refCollider);
    }

    public void UnActivate(BaseCollider _refCollider)
    {
        m_listPendingDelete.Add(_refCollider);
    }

    // 레이어 리스트/위치 추적에서 스왑백 제거, 겹쳐있던 쌍은 Exit 발화 후 정리
    private void DeleteCollider(BaseCollider _refCollider)
    {
        int iID = _refCollider.ID;
        int iMyIndex = m_listIndexInLayerList[iID];
        if (iMyIndex < 0)
            return; // 이미 처리됨 (중복 예약 가드)

        HashSet<int> hashOther = m_listOther[iID];
        foreach (int iOtherID in hashOther)
        {
            long lKey = MakePairKey(iID, iOtherID);
            if (m_hashPairInfo.TryGetValue(lKey, out tColliderPair tPair))
            {
                tPair.ColliderA.OnExitCollider(tPair.ColliderB);
                tPair.ColliderB.OnExitCollider(tPair.ColliderA);
                m_hashPairInfo.Remove(lKey);
            }
            m_listOther[iOtherID].Remove(iID);
        }

        hashOther.Clear();

        List<BaseCollider> listLayer = m_arrCollider[_refCollider.Layer];
        int iLastIndex = listLayer.Count - 1;

        BaseCollider refMoved = listLayer[iLastIndex];
        listLayer[iMyIndex] = refMoved;
        listLayer.RemoveAt(iLastIndex);
        m_listIndexInLayerList[refMoved.ID] = iMyIndex;
        m_listIndexInLayerList[iID] = -1;

        // 파괴 전에 반드시 빼야 다음 프레임 위치 갱신 Job이 죽은 Transform을 참조하지 않는다
        m_refCenterRefresher.Unregister(iID);
    }

    // ColliderManagerScheduler([DefaultExecutionOrder(-1000)])가 이 프레임에서 가장 먼저
    // 호출한다 - 충돌 Job이 이번 프레임 나머지 Update + LateUpdate 전체 동안 워커 스레드에서
    // 겹쳐 돌 수 있도록(클래스 헤더 참고)
    public void ScheduleFrame()
    {
        // 삭제 정리가 먼저 와야 이번 프레임 그리드/SoA에 죽은 콜라이더가 안 섞인다
        for (int i = 0; i < m_listPendingDelete.Count; ++i)
        {
            BaseCollider refCollider = m_listPendingDelete[i];

            // 예약 이후 재사용으로 다시 활성화됐으면 지우면 안 됨
            if (refCollider.gameObject.activeInHierarchy == false)
                DeleteCollider(refCollider);
        }

        m_listPendingDelete.Clear();

        PreLoadCenter();
        BuildGrid();
        ScheduleGridJob();
    }

    private void LateUpdate()
    {
        CompleteAndDrainGridJob();
    }

    // 위치/축 갱신은 ColliderCenterRefresher에 위임. CachedCenter를 읽는 외부 코드
    // (RaycastMask/FindAllInRadius/Aim 등)가 여전히 동기 프로퍼티로 읽을 수 있어야 하므로
    // Job 완료 즉시 콜라이더별로 Apply해서 되돌려 쓴다
    private void PreLoadCenter()
    {
        using (s_tMarkerRefreshCenter.Auto())
        {
            m_refCenterRefresher.ScheduleAndComplete();

            for (int iLayer = 0; iLayer < LAYER_COUNT; ++iLayer)
            {
                List<BaseCollider> listLayer = m_arrCollider[iLayer];
                for (int i = 0; i < listLayer.Count; ++i)
                    m_refCenterRefresher.Apply(listLayer[i]);
            }
        }
    }

    // 활성 콜라이더 전부를 SoA에 채우고 같은 순서로 그리드에 넣는다. 그리드는 한 번만 지어지고
    // ("정적 분할") 이후 매 프레임 셀 소속만 다시 계산한다
    private void BuildGrid()
    {
        int iTotalActive;

        using (s_tMarkerGather.Auto())
        {
            iTotalActive = 0;
            for (int iLayer = 0; iLayer < LAYER_COUNT; ++iLayer)
                iTotalActive += m_arrCollider[iLayer].Count;

            m_iCount = 0;
            if (iTotalActive == 0)
                return;

            ResizeSoaCapacity(iTotalActive);

            if (m_grid.IsBuilt == false)
            {
                m_listActiveCollider.Clear();
                for (int iLayer = 0; iLayer < LAYER_COUNT; ++iLayer)
                    m_listActiveCollider.AddRange(m_arrCollider[iLayer]);

                m_grid.Build(m_listActiveCollider);
            }
        }

        if (m_grid.IsBuilt == false)
            return;

        using (s_tMarkerGridBuild.Auto())
        {
            m_grid.BeginRebuild(iTotalActive);

            bool bCullByRange = m_refPlayer != null;
            Vector3 vPlayerPos = bCullByRange ? m_refPlayer.position : Vector3.zero;
            float fMaxRangeSq = m_fMaxBulletRange * m_fMaxBulletRange;

            int iIdx = 0;
            for (int iLayer = 0; iLayer < LAYER_COUNT; ++iLayer)
            {
                List<BaseCollider> listLayer = m_arrCollider[iLayer];
                for (int i = 0; i < listLayer.Count; ++i)
                {
                    BaseCollider refCollider = listLayer[i];
                    Vector3 vCenter = refCollider.CachedCenter;
                    bool bIsBox = refCollider.Shape == eColliderShape.Box;

                    // Box(Obstacle)만 플레이어 사거리 밖이면 이번 프레임 대상에서 제외
                    if (bIsBox && bCullByRange && (vCenter - vPlayerPos).sqrMagnitude > fMaxRangeSq)
                        continue;

                    m_arrCenter[iIdx] = vCenter;
                    m_arrBoundingRadius[iIdx] = refCollider.BoundingRadius;
                    m_arrColliderId[iIdx] = refCollider.ID;
                    m_arrColliderType[iIdx] = (int)refCollider.Shape;
                    m_arrLayer[iIdx] = refCollider.Layer;
                    m_arrHasPair[iIdx] = m_listOther[refCollider.ID].Count > 0;

                    if (bIsBox)
                    {
                        ObbCollider refBox = (ObbCollider)refCollider;
                        m_arrAxisX[iIdx] = refBox.AxisX;
                        m_arrAxisY[iIdx] = refBox.AxisY;
                        m_arrAxisZ[iIdx] = refBox.AxisZ;
                        m_arrHalfExtent[iIdx] = refBox.HalfExtent;
                    }

                    m_grid.AddCollider(iIdx, vCenter);
                    ++iIdx;
                }
            }

            m_grid.EndRebuild();
            m_iCount = iIdx;
        }
    }

    // 그리드가 곧 조회 대상이자 조회 주체라 레이어별로 나눠 여러 번 스케줄할 필요가 없다
    private void ScheduleGridJob()
    {
        using (s_tMarkerGridSchedule.Auto())
        {
            if (m_iCount == 0)
            {
                m_bScheduled = false;
                return;
            }

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
                Layer = m_arrLayer,
                HasPair = m_arrHasPair,

                LayerMatrixValue = m_arrLayerMatrixValue,

                Output = m_queResult.AsParallelWriter()
            };

            m_tJobHandle = tJob.Schedule(m_iCount, JOB_BATCH_SIZE);
            m_bScheduled = true;

            // 워커 스레드가 Complete까지 기다리지 않고 지금 바로 집어가게 한다
            JobHandle.ScheduleBatchedJobs();
        }
    }

    // Job 결과를 BaseCollider로 되돌려 Enter/Stay/Exit을 발화시키는 유일한 지점 - 낮은
    // 레이어가 항상 CheckPair의 A로 들어가도록 정렬해 콜백 발화 순서(A->B)를 안정적으로 유지
    private void CompleteAndDrainGridJob()
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
                BaseCollider refA = GetColliderByID(tResult.IdA);
                BaseCollider refB = GetColliderByID(tResult.IdB);

                if (refA == null || refB == null)
                    continue;

                if (refA.Layer < refB.Layer)
                    CheckPair(refA, refB, tResult.Overlap);
                else
                    CheckPair(refB, refA, tResult.Overlap);
            }
        }
    }

    private BaseCollider GetColliderByID(int _iID)
    {
        if (_iID < 0 || _iID >= m_listColliderByID.Count)
            return null;

        return m_listColliderByID[_iID];
    }

    // 겹침 여부를 이미 아는 호출부(Job 결과 드레인)를 위해 판정 결과를 인자로 받는다 -
    // 메인스레드에서 같은 수학을 두 번 돌리지 않기 위함
    private void CheckPair(BaseCollider _refA, BaseCollider _refB, bool _bOverlapping)
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
            // 안 겹치는 쌍이 압도적으로 많은 구조라 이 얼리아웃이 비용을 크게 아낌
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


    // ---- 도형별 겹침 판정 (raw-parameter, 관리형 참조 없음 - Burst Job이 그대로 호출) ----

    public static bool IsCircleCircleOverlap(
        Vector3 _vCenterA, float _fRadiusA, Vector3 _vCenterB, float _fRadiusB)
    {
        Vector3 vDelta = _vCenterB - _vCenterA;
        float fDistSq = vDelta.sqrMagnitude;
        float fRadiusSum = _fRadiusA + _fRadiusB;
        return fDistSq <= fRadiusSum * fRadiusSum;
    }

    // 구 중심을 박스 로컬 축 3개에 투영 -> half-extent로 클램프 -> 델타 제곱합을 반지름 제곱과 비교
    public static bool IsCircleBoxOverlap(
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

    // Physics.Raycast 대체용(PhysX 없음). Circle 전용
    public bool RaycastMask(Vector3 _vOrigin, Vector3 _vDir, float _fMaxLength, LayerMask _tMask, out CircleCollider _refHit)
    {
        _vDir.Normalize();

        CircleCollider refClosest = null;
        float fClosestT = float.MaxValue;

        for (int iLayer = 0; iLayer < LAYER_COUNT; ++iLayer)
        {
            if ((_tMask.value & (1 << iLayer)) == 0)
                continue;

            List<BaseCollider> listLayer = m_arrCollider[iLayer];
            for (int i = 0; i < listLayer.Count; ++i)
            {
                if (!(listLayer[i] is CircleCollider refCollider))
                    continue;

                Vector3 vToCenter = refCollider.CachedCenter - _vOrigin;

                float fT = Mathf.Clamp(Vector3.Dot(vToCenter, _vDir), 0f, _fMaxLength);
                Vector3 vClosePoint = _vOrigin + _vDir * fT;
                float fDistSq = (vClosePoint - refCollider.CachedCenter).sqrMagnitude;

                if (fDistSq <= refCollider.Radius * refCollider.Radius && fT < fClosestT)
                {
                    fClosestT = fT;
                    refClosest = refCollider;
                }
            }
        }

        _refHit = refClosest;
        return refClosest != null;
    }

    // Physics.OverlapSphereNonAlloc 대체용(PhysX 없음). Circle 전용, 무할당
    public void FindAllInRadius(Vector3 _vPos, float _fRadius, LayerMask _tMask, List<CircleCollider> _listResult)
    {
        _listResult.Clear();
        float fRadiusSq = _fRadius * _fRadius;

        for (int iLayer = 0; iLayer < LAYER_COUNT; ++iLayer)
        {
            if ((_tMask.value & (1 << iLayer)) == 0)
                continue;

            List<BaseCollider> listLayer = m_arrCollider[iLayer];
            for (int i = 0; i < listLayer.Count; ++i)
            {
                if (!(listLayer[i] is CircleCollider refCollider))
                    continue;

                float fDistSq = (refCollider.CachedCenter - _vPos).sqrMagnitude;

                if (fDistSq <= fRadiusSq)
                    _listResult.Add(refCollider);
            }
        }
    }

    // Physics.OverlapSphere 대체용(PhysX 없음). Circle 전용, 범위 내 최근접 하나
    public bool FindNearest(Vector3 _vPos, float _fRadius, LayerMask _tMask, out CircleCollider _refHit)
    {
        CircleCollider refNearest = null;
        float fNearestDistSq = float.MaxValue;
        float fRadiusSq = _fRadius * _fRadius;

        for (int iLayer = 0; iLayer < LAYER_COUNT; ++iLayer)
        {
            if ((_tMask.value & (1 << iLayer)) == 0)
                continue;

            List<BaseCollider> listLayer = m_arrCollider[iLayer];
            for (int i = 0; i < listLayer.Count; ++i)
            {
                if (!(listLayer[i] is CircleCollider refCollider))
                    continue;

                float fDistSq = (refCollider.CachedCenter - _vPos).sqrMagnitude;

                if (fDistSq <= fRadiusSq && fDistSq < fNearestDistSq)
                {
                    fNearestDistSq = fDistSq;
                    refNearest = refCollider;
                }
            }
        }

        _refHit = refNearest;
        return refNearest != null;
    }


    // ---- NativeContainer 관리 (프레임 스크래치라 매 프레임 통째로 덮어씀, 보존 불필요) ----

    private void ResizeSoaCapacity(int _iCount)
    {
        if (m_arrCenter.IsCreated && m_arrCenter.Length >= _iCount)
            return;

        int iNewCapacity = m_arrCenter.IsCreated ? m_arrCenter.Length : INITIAL_CAPACITY;
        while (iNewCapacity < _iCount)
            iNewCapacity <<= 1;

        DisposeSoa();
        AllocateSoa(iNewCapacity);
    }

    private void AllocateSoa(int _iCapacity)
    {
        m_arrCenter = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        m_arrAxisX = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        m_arrAxisY = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        m_arrAxisZ = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        m_arrHalfExtent = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        m_arrBoundingRadius = new NativeArray<float>(_iCapacity, Allocator.Persistent);
        m_arrColliderId = new NativeArray<int>(_iCapacity, Allocator.Persistent);
        m_arrColliderType = new NativeArray<int>(_iCapacity, Allocator.Persistent);
        m_arrLayer = new NativeArray<int>(_iCapacity, Allocator.Persistent);
        m_arrHasPair = new NativeArray<bool>(_iCapacity, Allocator.Persistent);
    }

    private void DisposeSoa()
    {
        if (m_arrCenter.IsCreated)
            m_arrCenter.Dispose();
        if (m_arrAxisX.IsCreated)
            m_arrAxisX.Dispose();
        if (m_arrAxisY.IsCreated)
            m_arrAxisY.Dispose();
        if (m_arrAxisZ.IsCreated)
            m_arrAxisZ.Dispose();
        if (m_arrHalfExtent.IsCreated)
            m_arrHalfExtent.Dispose();
        if (m_arrBoundingRadius.IsCreated)
            m_arrBoundingRadius.Dispose();
        if (m_arrColliderId.IsCreated)
            m_arrColliderId.Dispose();
        if (m_arrColliderType.IsCreated)
            m_arrColliderType.Dispose();
        if (m_arrLayer.IsCreated)
            m_arrLayer.Dispose();
        if (m_arrHasPair.IsCreated)
            m_arrHasPair.Dispose();
    }

    // ---- Job ----

    // 그리드에 들어간 콜라이더 하나(index)당 이웃 27칸의 후보만 검사한다. 후보 index가
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

            BoxColliderGrid.ComputeCellCoord(vMyCenter, GridOrigin, CellSize, CountX, CountY, CountZ,
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
                        int iCell = BoxColliderGrid.FlattenIndex(ix, iy, iz, CountX, CountY);
                        int iStart = CellStart[iCell];
                        int iEnd = iStart + CellCount[iCell];

                        for (int k = iStart; k < iEnd; ++k)
                        {
                            int j = CellItems[k];

                            if (j <= index)
                                continue;

                            if (!ColliderManager.IsLayerCollider(LayerMatrixValue, iMyLayer, Layer[j]))
                                continue;

                            int iOtherType = ColliderType[j];
                            Vector3 vOtherCenter = Center[j];
                            float fOtherRadius = BoundingRadius[j];

                            bool bOverlap;
                            if (iMyType != COLLIDER_TYPE_BOX && iOtherType != COLLIDER_TYPE_BOX)
                                bOverlap = ColliderManager.IsCircleCircleOverlap(vMyCenter, fMyRadius, vOtherCenter, fOtherRadius);
                            else if (iMyType == COLLIDER_TYPE_BOX && iOtherType == COLLIDER_TYPE_BOX)
                                bOverlap = false; // Box-Box (지금 매트릭스엔 없음, 방어적 스텁)
                            else
                            {
                                // 구-구 선판정으로 먼저 거른다 - 통과 못 하면 진짜 OBB 판정 없이도 100% 안 겹침
                                float fBoundSum = fMyRadius + fOtherRadius;
                                Vector3 vDelta = vOtherCenter - vMyCenter;
                                bOverlap = false;

                                if (vDelta.sqrMagnitude <= fBoundSum * fBoundSum)
                                {
                                    bOverlap = iMyType == COLLIDER_TYPE_BOX
                                        ? ColliderManager.IsCircleBoxOverlap(
                                            vOtherCenter, fOtherRadius, vMyCenter, AxisX[index], AxisY[index], AxisZ[index], HalfExtent[index])
                                        : ColliderManager.IsCircleBoxOverlap(
                                            vMyCenter, fMyRadius, vOtherCenter, AxisX[j], AxisY[j], AxisZ[j], HalfExtent[j]);
                                }
                            }

                            // 안 겹친 결과는 "저번 프레임까지 겹쳐있던 쌍"의 Exit 판정에만 필요
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
