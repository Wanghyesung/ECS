using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;

/*///////////////////////////////////////////
              ColliderManager
목적 : BaseCollider(Circle/Obb)들을 PhysX 없이 자체적으로 충돌 판정한다.

       - 레이어 0~31마다 List<BaseCollider>를 Awake에서 미리 만들어두고, 어떤 레이어끼리
         충돌할지는 m_arrLayerCollisionMatrix로 이 매니저가 중앙에서 결정한다.
       - 레이어/도형 구분 없이 활성 콜라이더 전부를 하나의 SoA + 하나의 BoxColliderGrid에
         담는다. "누가 그리드를 소유하는가" 같은 구분이 없다 - 그리드에 들어간 콜라이더
         자신이 곧 조회 주체다(자기 배열 안에서 이웃 27칸을 찾고, 후보 인덱스가 자기보다
         크면(j > i) 검사, 작거나 같으면 스킵 - 이미 그 반대 방향에서 검사됐거나 자기 자신).
       - Job(GridOverlapJob)의 Execute(int index) 하나가 이웃 셀 탐색 + 레이어 매트릭스
         필터 + 도형 조합(Circle-Circle/Circle-Box/Box-Box) 분기 + 실제 판정 함수 호출까지
         전부 담당한다. 메인스레드는 매 프레임 (위치, BoundingRadius, 도형별 축/halfExtent,
         타입, ID, 레이어, 기존 겹침 여부)만 채워 넘기고 그 이상의 절차적 로직을 갖지 않는다.
       - Job은 관리형 타입을 못 만지므로 이 데이터를 NativeArray SoA로 복사해 넘기고,
         Job은 "겹쳤다/안 겹쳤다"만 NativeQueue에 담는다. BaseCollider 해석과 Enter/Stay/Exit
         콜백은 전부 Complete 이후 메인스레드 드레인 구간에서만 일어난다.
       - 스케줄은 Update()에서 미리 시작하고 LateUpdate()에서 결과만 받는다(PreLoadCenter->
         BuildGrid->Schedule은 Update, Complete+드레인은 LateUpdate) - Bullet/Missile/Guided의
         이동 Job과 완전히 같은 패턴. Update() 시점엔 이번 프레임 이동이 아직 하나도 반영
         안 된 상태다(Bullet 등은 Update에서 이동 Job을 Schedule만 하고 실제 위치 쓰기는
         자기 LateUpdate의 Complete에서 하므로) - 그래서 여기서 캐싱하는 위치는 "저번 프레임
         LateUpdate가 끝난 시점의 스냅샷"이고, 이번 프레임 판정은 의도적으로 한 프레임 늦은
         데이터를 쓴다. 이렇게 미루는 이유 둘:
         (1) Schedule 직후 바로 Complete하면 워커 스레드가 도는 동안 메인스레드가 할 일이
             없어 순수 대기가 된다(실측 GridJobComplete≈2.5ms 전부 블로킹). Update에서 미리
             Schedule해두면 이번 프레임 나머지 Update + LateUpdate 전체 구간(다른 시스템의
             이동 Complete, 카메라, UI 등) 동안 워커 스레드에서 겹쳐 돌 시간을 벌어, 실제
             LateUpdate에서의 Complete 대기 시간을 줄인다.
         (2) 한 프레임의 판정 결과(=그 프레임에 실행되는 모든 Enter/Stay/Exit)가 전부 "동일한
             한 시점의 스냅샷"을 기준으로 계산된다 - 콜라이더 콜백 안에 상태 전환 로직(예:
             피격 시 방어 상태로 전환)이 있어도, 그 로직이 이번 프레임 판정 자체에 영향을
             주지 않는다(판정은 이미 스냅샷 시점에 다 끝나 있음). "누가 먼저 콜백을 받아
             상태를 바꾸느냐"에 따라 같은 프레임의 다른 판정 결과가 갈리는 문제가 구조적으로 없다.
         대가: 충돌 판정이 실제 최신 위치보다 정확히 한 프레임(약 16ms@60fps) 늦게 반영된다.
       - m_hashPairInfo에 쌍 항목이 있다 = 지금 겹치는 중. CheckPair 하나가 Enter/Stay/Exit을
         그 자리에서 전부 판정하므로 "지우는 걸 깜빡한다" 류의 버그가 구조적으로 없다.
       - 엔진은 한 프레임 안에서 모든 스크립트의 Update()를 끝낸 뒤에야 모든 LateUpdate()를
         시작하는 페이즈 순서를 고정한다 - 그래서 Update에서 Schedule한 Job이 LateUpdate
         페이즈 전체 동안 워커 스레드에서 돌 수 있다.
 *///////////////////////////////////////////

// Update에서 Schedule한 충돌 Job을 LateUpdate에서 Complete하는데, LateUpdate가 이 프레임의
// 모든 스크립트 중 가장 늦게 돌수록 그만큼 Job이 워커 스레드에서 겹쳐 돌 시간이 길어져서
// Complete 대기가 짧아진다(클래스 헤더 참고) - 그래서 기본 실행 순서보다 뒤로 고정해둔다.
// 판정 자체는 한 프레임 늦은 스냅샷 기준이라 다른 시스템과의 순서에 더 이상 정확성 의존이 없음
[DefaultExecutionOrder(1000)]
public class ColliderManager : MonoBehaviour
{
    public static ColliderManager m_Instance = null;

    private const int LAYER_COUNT = 32;
    private const int JOB_BATCH_SIZE = 64;
    private const int INITIAL_CAPACITY = 1024;
    // eColliderShape.Box의 정수값 - SoA에 담긴 ColliderType과 비교할 때 쓰는 상수(Burst Job에서도 동일)
    private const int COLLIDER_TYPE_BOX = (int)eColliderShape.Box;

    // m_arrLayerCollisionMatrix[i] = 레이어 i가 충돌할 레이어 마스크. 한쪽만 등록해도
    // 인식됨(IsLayerCollider 참고), Unity Physics 매트릭스와 동일한 개념
    [SerializeField] private LayerMask[] m_arrLayerCollisionMatrix = new LayerMask[LAYER_COUNT];

    // Box(Obstacle) 컬링 기준점. 비워두면 컬링 없이 전부 판정 대상(안전한 기본값).
    // Circle에는 적용 안 함 - 사거리 밖 장애물만 그리드에서 빠진다
    [SerializeField] private Transform m_refPlayer;
    // 플레이어의 총알이 도달할 수 있는 최대 거리 - 이보다 먼 Box는 그리드에서 제외됨
    [SerializeField] private float m_fMaxBulletRange = 1000f;

    // 레이어(0~31) -> 그 레이어에 속한 활성 콜라이더 목록. Circle/Obb 공용 -
    // 레이어 하나엔 한 가지 도형만 들어간다는 게 전제(§도형별 겹침 판정 참고)
    private List<BaseCollider>[] m_arrCollider;

    // UnActivate 예약 - 다음 프레임 Update() 맨 앞에서 일괄 스왑백 제거(DeleteCollider 참고)
    private List<BaseCollider> m_listPendingDelete;

    // ID -> 그 콜라이더가 자기 레이어 리스트에서 몇 번째 자리인지 (스왑백 O(1) 제거용)
    private List<int> m_listIndexInLayerList;

    // ID -> BaseCollider 본체. Job이 돌려주는 결과(ID 쌍)를 실제 객체로 되돌릴 때만 쓴다
    private List<BaseCollider> m_listColliderByID;

    // ID -> 지금 이 ID와 실제로 겹쳐있다고 기록된 상대 ID들. UnActivate 시 관련 쌍 정리용
    private List<HashSet<int>> m_listOther;

    // 쌍(A,B) 항목 존재 = 지금 겹치는 중. CheckPair가 이 하나로 Enter/Stay/Exit 전부
    // 처리, Circle/Box 구분 없이 공용(ID 공간을 BaseCollider가 공유해서 가능)
    private Dictionary<long, tColliderPair> m_hashPairInfo;

    // Stay만 별도 마커로 뗀 이유 - CheckPair 전체를 감싸면 프레임당 수만 콜에 마커
    // 오버헤드가 측정을 왜곡하지만, Stay 발생 쌍은 상대적으로 적어서 감싸도 괜찮음
    private static readonly ProfilerMarker s_tMarkerStay = new ProfilerMarker("ColliderManager.OnStay");

    // LateUpdate 구간별 비용을 나눠보기 위한 마커 - 전부 프레임당 한 번만 불려서
    // 마커 오버헤드가 측정을 왜곡할 걱정은 없음
    private static readonly ProfilerMarker s_tMarkerRefreshCenter = new ProfilerMarker("ColliderManager.PreLoadCenter");
    private static readonly ProfilerMarker s_tMarkerGather = new ProfilerMarker("ColliderManager.Gather");
    private static readonly ProfilerMarker s_tMarkerGridBuild = new ProfilerMarker("ColliderManager.GridBuild");
    private static readonly ProfilerMarker s_tMarkerGridSchedule = new ProfilerMarker("ColliderManager.GridJobSchedule");
    private static readonly ProfilerMarker s_tMarkerGridComplete = new ProfilerMarker("ColliderManager.GridJobComplete");
    private static readonly ProfilerMarker s_tMarkerGridDrain = new ProfilerMarker("ColliderManager.GridJobDrain");

    // 활성 콜라이더 전부가 들어가는 단일 공간 그리드. Shape/레이어 구분 없이 하나
    private BoxColliderGrid m_grid;

    // m_grid.Build()에 넘길 List<BaseCollider>가 필요할 때(그리드가 아직 안 지어졌을 때,
    // 생애주기 중 최대 한 번)만 채우는 일회성 스크래치 - 매 프레임 다시 채우지 않는다
    // (재할당 없이 재사용하려고 필드로 둠). SoA/그리드 자체는 m_arrCollider[layer]를 매
    // 프레임 직접 순회해서 채운다(BuildGrid 참고)
    private List<BaseCollider> m_listAllActive;

    // --- Job 입력용 SoA (전부 Allocator.Persistent, 매 프레임 통째로 덮어씀) ---
    // AxisX/Y/Z/HalfExtent는 ColliderType==Box일 때만 의미 있는 값이 채워지고,
    // Circle 항목은 기본값(0벡터)으로 남아 Job이 아예 안 읽는다
    private NativeArray<Vector3> m_arrCenter;
    private NativeArray<Vector3> m_arrAxisX;
    private NativeArray<Vector3> m_arrAxisY;
    private NativeArray<Vector3> m_arrAxisZ;
    private NativeArray<Vector3> m_arrHalfExtent;
    private NativeArray<float> m_arrBoundingRadius;
    private NativeArray<int> m_arrColliderId;
    // eColliderShape 값(0=Circle,1=Box) - GridOverlapJob이 이 값으로 Box/Circle 분기
    private NativeArray<int> m_arrColliderType;
    private NativeArray<int> m_arrLayer;
    // "이 콜라이더가 지금 뭐라도 겹쳐있다고 기록돼 있는가" - 안 겹친 결과를 굳이 큐에 담을지
    // 결정하는 필터. 기록이 없으면 Exit이 나올 수 없으므로 안 담아도 결과가 완전히 동일하다
    private NativeArray<bool> m_arrHasPair;
    private int m_iCount;

    // 레이어 매트릭스를 Job(Burst)이 읽을 수 있는 값 배열로 한 번만 복사해둔 것.
    // m_arrLayerCollisionMatrix는 인스펙터에서 설정되고 런타임에 안 바뀌므로 Awake에서 한 번이면 충분
    private NativeArray<int> m_arrLayerMatrixValue;

    // Job 결과. NativeList.ParallelWriter(AddNoResize)와 달리 사전 용량 예약이 필요 없어서,
    // 최악의 프레임에도 결과가 조용히 유실될 위험이 없다
    private NativeQueue<tPairResult> m_queResult;

    private JobHandle m_tJobHandle;
    private bool m_bScheduled;

    private struct tColliderPair
    {
        public BaseCollider ColliderA;
        public BaseCollider ColliderB;
    }

    // Job이 돌려주는 결과 한 건. Overlap=false 항목은 "후보였는데 안 겹쳤다"는 뜻으로,
    // 저번 프레임까지 겹쳐있던 쌍의 Exit을 놓치지 않기 위해 필요하다
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

        m_arrCollider = new List<BaseCollider>[LAYER_COUNT];
        for (int i = 0; i < LAYER_COUNT; ++i)
            m_arrCollider[i] = new List<BaseCollider>();

        m_grid = new BoxColliderGrid();
        m_listAllActive = new List<BaseCollider>();

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

    // 진행 중인 Job을 먼저 끝내고(워커가 이미 Dispose된 메모리를 만지지 않도록)
    // 모든 NativeContainer를 해제한다. 중복 인스턴스는 Awake에서 아무것도 할당하지
    // 않고 빠져나갔으므로 IsCreated 가드에 전부 걸러진다
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

        m_grid?.Dispose();
    }

    private static long MakePairKey(int _iA, int _iB)
    {
        int iLow = _iA < _iB ? _iA : _iB;
        int iHigh = _iA < _iB ? _iB : _iA;
        return ((long)iLow << 32) | (uint)iHigh;
    }

    // 레이어 A가 레이어 B와 충돌하는지. Unity Physics 매트릭스처럼 어느 한쪽 방향만
    // 등록해도 인식됨. Burst Job(GridOverlapJob)이 그대로 호출할 수 있도록 관리형 참조
    // 없이 값 배열(NativeArray<int>)만 받는 raw-parameter 버전 - 씬 없이 EditMode 테스트로도 검증 가능
    public static bool IsLayerCollider(NativeArray<int> _arrMatrixValue, int _iLayerA, int _iLayerB)
    {
        bool bAToB = (_arrMatrixValue[_iLayerA] & (1 << _iLayerB)) != 0;
        bool bBToA = (_arrMatrixValue[_iLayerB] & (1 << _iLayerA)) != 0;
        return bAToB || bBToA;
    }

    // ID 기준 보조 리스트 크기를 맞춰줌 (등록 순서 = ID 순서라 사실상 Add와 동일하게 채워짐)
    private void ResizeCapacity(int _iID)
    {
        while (m_listIndexInLayerList.Count <= _iID)
        {
            m_listIndexInLayerList.Add(-1);
            m_listColliderByID.Add(null);
            m_listOther.Add(new HashSet<int>());
        }
    }

    // BaseCollider.Start()에서 생애주기 중 딱 한 번만 호출. 레이어 리스트엔 아직 안 들어감(Activate가 담당)
    public void RegisterCollider(BaseCollider _refCollider)
    {
        ResizeCapacity(_refCollider.ID);
        m_listColliderByID[_refCollider.ID] = _refCollider;
    }

    // BaseCollider.OnEnable()/Start()에서 호출 - 그 즉시 자기 레이어 리스트에 편입.
    // 재발사 등으로 죽은 직후 다시 활성화될 때 중복 등록(유령 항목) 방지 - 이미 등록돼 있으면 무시
    public void Activate(BaseCollider _refCollider)
    {
        ResizeCapacity(_refCollider.ID);

        // OnEnable이 Start(=RegisterCollider)보다 먼저 도는 경로가 있어서 여기서도 채워둔다 -
        // Job 결과를 객체로 되돌릴 때 이 표가 비어 있으면 그 쌍이 통째로 무시되기 때문
        m_listColliderByID[_refCollider.ID] = _refCollider;

        if (m_listIndexInLayerList[_refCollider.ID] >= 0)
            return;

        List<BaseCollider> listLayer = m_arrCollider[_refCollider.Layer];
        m_listIndexInLayerList[_refCollider.ID] = listLayer.Count;
        listLayer.Add(_refCollider);
    }

    // BaseCollider.OnDisable()에서 호출. 레이어 리스트 제거는 즉시 안 하고 다음 프레임
    // Update()로 예약만 함 - 판정 순회 중 스왑백하면 방금 그 자리로 옮겨온 콜라이더가
    // 이번 프레임 판정에서 통째로 스킵될 수 있기 때문
    public void UnActivate(BaseCollider _refCollider)
    {
        m_listPendingDelete.Add(_refCollider);
    }

    // 예약된 콜라이더를 레이어 리스트에서 스왑백 제거, ColliderExit호출, 충돌 쌍 제거
    private void DeleteCollider(BaseCollider _refCollider)
    {
        int iID = _refCollider.ID;
        int iMyIndex = m_listIndexInLayerList[iID];
        if (iMyIndex < 0)
            return; // 이미 처리됨 (중복 예약 가드)

        // 관여하던 쌍 기록을 전부 정리(겹친 채로 반납되면 Exit도 쏴줌) - Circle/Box 구분 없이 동일 처리
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

        // 그리드는 매 프레임 통째로 재구성되므로(BuildGrid) 여기서 따로 지울 게 없다 -
        // 다음 재구성 때 이 콜라이더가 레이어 리스트에 없어서 자연히 빠진다

        List<BaseCollider> listLayer = m_arrCollider[_refCollider.Layer];
        int iLastIndex = listLayer.Count - 1;

        BaseCollider refMoved = listLayer[iLastIndex];
        listLayer[iMyIndex] = refMoved;
        listLayer.RemoveAt(iLastIndex);
        m_listIndexInLayerList[refMoved.ID] = iMyIndex;
        m_listIndexInLayerList[iID] = -1;
    }

    // 삭제 정리 -> PreLoadCenter -> BuildGrid -> Job Schedule까지 전부 여기서 미리 끝내둔다.
    // LateUpdate가 아니라 Update에서 Schedule하는 이유는 클래스 헤더 주석 참고 - "한 프레임
    // 늦은 스냅샷으로 판정"을 의도적으로 선택해 Job이 이번 프레임 나머지 구간에서 겹쳐 돌게 함
    private void Update()
    {
        // 삭제 정리가 Gather보다 먼저 와야 이번 프레임 그리드/SoA에 죽은 콜라이더가 안 섞인다
        for (int i = 0; i < m_listPendingDelete.Count; ++i)
        {
            BaseCollider refCollider = m_listPendingDelete[i];

            // 예약 이후 재사용으로 다시 활성화됐으면 지우면 안 됨 - 실제로 비활성인 것만 삭제
            if (refCollider.gameObject.activeInHierarchy == false)
                DeleteCollider(refCollider);
        }

        m_listPendingDelete.Clear();

        PreLoadCenter();
        BuildGrid();
        ScheduleGridJob();
    }

    // Update에서 Schedule해둔 Job을 Complete하고 결과를 드레인한다. Bullet/Missile/Guided의
    // 이동 Job이 각자의 LateUpdate에서 위치를 확정하는 것과 같은 타이밍 - 이 시점엔 이미 이번
    // 프레임 이동이 다 반영돼 있지만, 우리가 스케줄한 Job은 그 이동이 반영되기 "전" 스냅샷
    // 기준으로 계산된 것이라 결과 자체는 여전히 한 프레임 늦다(의도된 동작, 클래스 헤더 참고)
    private void LateUpdate()
    {
        CompleteAndDrainGridJob();
    }

    // 활성 콜라이더 전부(레이어/도형 무관)를 SoA에 채우고 같은 순서로 그리드에 넣는다.
    // 그리드는 한 번만 지어지고("정적 분할", BoxColliderGrid.Build) 그 뒤로 매 프레임
    // 셀 소속을 통째로 다시 계산한다. 사거리 밖(컬링) Box는 그냥 이번 프레임 그리드/SoA
    // 대상에서 빠진다(Circle엔 컬링 미적용 - 기존 동작 그대로 보존).
    //
    // m_arrCollider[layer]는 이미 Activate/DeleteCollider로 "활성 콜라이더만" 항상 유지되는
    // 배열이라(§Activate/DeleteCollider 참고), 매 프레임 이걸 다시 하나의 리스트로 복사할
    // 필요가 없다 - List<T>.Count는 O(1)이라 전체 개수는 32개 레이어를 훑는 것만으로 구해지고,
    // SoA/그리드는 이 32개 리스트를 그대로 순회하며 직접 채운다. m_listAllActive는 오직
    // "그리드가 아직 한 번도 안 지어졌을 때"(생애주기 중 딱 한 번)만 BoxColliderGrid.Build()에
    // 넘길 List<BaseCollider>가 필요해서 그때만 채우는 일회성 스크래치다
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
                m_listAllActive.Clear();
                for (int iLayer = 0; iLayer < LAYER_COUNT; ++iLayer)
                    m_listAllActive.AddRange(m_arrCollider[iLayer]);

                m_grid.Build(m_listAllActive);
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

                    //박스 형태, 플레이어 기준 컬링할지, 프로드 페이즈(원충돌)
                    if (bIsBox && bCullByRange && (vCenter - vPlayerPos).sqrMagnitude > fMaxRangeSq)
                        continue;

                    m_arrCenter[iIdx] = vCenter;
                    m_arrBoundingRadius[iIdx] = refCollider.BoundingRadius;
                    m_arrColliderId[iIdx] = refCollider.ID;
                    m_arrColliderType[iIdx] = (int)refCollider.Shape;
                    m_arrLayer[iIdx] = refCollider.Layer;
                    m_arrHasPair[iIdx] = m_listOther[refCollider.ID].Count > 0;

                    if (bIsBox == true)
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

    // 그리드에 들어간 콜라이더 전체(m_iCount개)에 대해 Job 하나를 스케줄한다.
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

    // Job 결과를 실제 BaseCollider로 되돌려 Enter/Stay/Exit을 발화시키는 유일한 지점.
    // 낮은 레이어가 항상 CheckPair의 A로 들어가도록 정렬 - 콜백 발화 순서(A->B)를 안정적으로 유지
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

    private void PreLoadCenter()
    {
        using (s_tMarkerRefreshCenter.Auto())
        {
            for (int iLayer = 0; iLayer < LAYER_COUNT; ++iLayer)
            {
                List<BaseCollider> listLayer = m_arrCollider[iLayer];
                for (int i = 0; i < listLayer.Count; ++i)
                {
                    if (listLayer[i].StaticObject == false)
                        listLayer[i].RefreshCenter();
                }
            }
        }
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
                // 이번 프레임에도 맞음 -> Stay
                using (s_tMarkerStay.Auto())
                {
                    tPair.ColliderA.OnStayCollider(tPair.ColliderB);
                    tPair.ColliderB.OnStayCollider(tPair.ColliderA);
                }
            }
            else
            {
                // 이번 프레임에 처음 맞음 -> Enter
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
            // A가 지금 아무와도 안 겹치면 이 쌍도 당연히 기록이 없으므로 조회 자체를 스킵 -
            // 안 겹치는 쌍이 압도적으로 많은 구조라(총알 vs 운석 등) 이 한 줄이 비용을 크게 아낌
            if (m_listOther[_refA.ID].Count == 0)
                return;

            long lKey = MakePairKey(_refA.ID, _refB.ID);
            if (m_hashPairInfo.TryGetValue(lKey, out tColliderPair tOld))
            {
                // 저번 프레임까진 맞았는데 이번에 떨어짐 -> Exit
                m_hashPairInfo.Remove(lKey);
                m_listOther[_refA.ID].Remove(_refB.ID);
                m_listOther[_refB.ID].Remove(_refA.ID);

                tOld.ColliderA.OnExitCollider(tOld.ColliderB);
                tOld.ColliderB.OnExitCollider(tOld.ColliderA);
            }
        }
    }


    // ---- 도형별 겹침 판정 (raw-parameter, 관리형 참조 없음 - Burst Job이 그대로 호출) ----

    // 원-원(구-구) 판정. 3D 전체 거리(X/Y/Z)로 겹침 판정. 씬 없이 EditMode 테스트로 검증 가능
    public static bool IsCircleCircleOverlap(
        Vector3 _vCenterA, float _fRadiusA, Vector3 _vCenterB, float _fRadiusB)
    {
        Vector3 vDelta = _vCenterB - _vCenterA;
        float fDistSq = vDelta.sqrMagnitude;
        float fRadiusSum = _fRadiusA + _fRadiusB;
        return fDistSq <= fRadiusSum * fRadiusSum;
    }

    // 원(구)-OBB 판정. 구 중심을 박스의 로컬 축 3개에 투영 -> half-extent로 클램프 ->
    // 델타 제곱합을 반지름 제곱과 비교. 씬 없이 EditMode 테스트로 검증 가능하도록 public static.
    // GridOverlapJob이 Burst로 컴파일해 그대로 호출하기도 한다(관리형 참조/예외 없음)
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

    // Physics.Raycast 대체용(PhysX 없음) - Aim 등 화면 좌표 기반 조준에서 사용. Circle 전용
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

    // Physics.OverlapSphereNonAlloc 대체용(PhysX 없음) - 범위 내 전체 목록. Circle 전용,
    // 호출부 재사용 리스트를 Clear 후 채우므로 무할당
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

    // Physics.OverlapSphere 대체용(PhysX 없음) - 범위 내 최근접 하나. Circle 전용
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


    // ---- NativeContainer 관리 ----

    // 프레임 스크래치 버퍼라 이전 내용을 보존할 필요가 없다(매 프레임 통째로 덮어씀) -
    // 모자라면 Dispose 후 더블링 크기로 새로 잡는다(복사 불필요).
    // 호출 시점은 반드시 "직전 프레임 Job이 Complete된 뒤"여야 한다 - Update()가 BuildGrid를
    // 통해 이 함수를 호출할 때는 항상 그 조건이 성립한다(직전 LateUpdate에서 이미 Complete됨)
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

    // NativeArray만 참조하는 순수 struct - class/List 등 관리 객체는 여기 들어올 수 없다.
    // 그리드에 들어간 콜라이더 하나(index)당 자기 셀 기준 이웃 27칸의 후보만 검사한다.
    // 후보 index가 자기(index) 이하면 스킵 - 자기 자신이거나, 이미 반대 방향에서 검사된 쌍이라
    // 별도의 "그리드 소유/조회" 구분이나 레이어별 중복 방지 가드가 필요 없다. 레이어 매트릭스
    // 필터와 도형 조합(Circle-Circle/Circle-Box/Box-Circle/Box-Box) 분기까지 전부 여기서 처리
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

                            // 자기 자신이거나 이미 반대 방향(j->index)에서 검사된 쌍 - 전역
                            // 인덱스 하나로만 비교하면 되므로 레이어/그리드 구분이 필요 없다
                            if (j <= index)
                                continue;

                            if (!ColliderManager.IsLayerCollider(LayerMatrixValue, iMyLayer, Layer[j]))
                                continue;

                            int iOtherType = ColliderType[j];
                            Vector3 vOtherCenter = Center[j];
                            float fOtherRadius = BoundingRadius[j];

                            bool bOverlap;
                            if (iMyType != COLLIDER_TYPE_BOX && iOtherType != COLLIDER_TYPE_BOX)
                            {
                                bOverlap = ColliderManager.IsCircleCircleOverlap(vMyCenter, fMyRadius, vOtherCenter, fOtherRadius);
                            }
                            else if (iMyType == COLLIDER_TYPE_BOX && iOtherType == COLLIDER_TYPE_BOX)
                            {
                                bOverlap = false; // Box-Box (지금 매트릭스엔 없음, 방어적 스텁)
                            }
                            else
                            {
                                // 구-구 선판정으로 먼저 거른다 - BoundingRadius는 넉넉한 상한이라,
                                // 통과 못 하면 진짜 OBB 판정 없이도 100% 안 겹침(오탐/누락 없음)
                                float fBoundSum = fMyRadius + fOtherRadius;
                                Vector3 vDelta = vOtherCenter - vMyCenter;
                                bOverlap = false;

                                if (vDelta.sqrMagnitude <= fBoundSum * fBoundSum)
                                {
                                    bOverlap = iMyType == COLLIDER_TYPE_BOX
                                        ? ColliderManager.IsCircleBoxOverlap(
                                            vOtherCenter, fOtherRadius,
                                            vMyCenter, AxisX[index], AxisY[index], AxisZ[index], HalfExtent[index])
                                        : ColliderManager.IsCircleBoxOverlap(
                                            vMyCenter, fMyRadius,
                                            vOtherCenter, AxisX[j], AxisY[j], AxisZ[j], HalfExtent[j]);
                                }
                            }

                            // 안 겹친 결과는 "저번 프레임까지 겹쳐있던 쌍"의 Exit 판정에만 필요하다 -
                            // 둘 다 겹침 기록 자체가 없으면 Exit이 나올 수 없으므로 큐에 안 담는다
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
