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
       - Circle-Circle 쌍(총알-몬스터 등)은 브루트포스가 그리드보다 빠르므로 메인스레드에서
         그대로 돌리고, Shape==Box가 낀 쌍만 BoxColliderGrid + Burst Job으로 브로드페이즈한다.
         m_refPlayer가 있으면 사거리(m_fMaxBulletRange) 밖 Box는 그리드에서 아예 빠진다.
       - Job은 관리형 타입을 못 만지므로 Box/Circle 정보를 NativeArray SoA로 복사해 넘기고,
         Job은 "겹쳤다/안 겹쳤다"만 NativeQueue에 담는다. BaseCollider 해석과 Enter/Stay/Exit
         콜백은 전부 Complete 이후 메인스레드 드레인 구간에서만 일어난다.
       - 스케줄 순서: PreLoadCenter -> UpdateBoxGrids(그리드 재구성 + Box SoA) -> 레이어
         매트릭스 수집 -> Job Schedule -> (Job이 워커에서 도는 동안) Circle-Circle
         브루트포스 -> Complete -> 결과 드레인.
       - m_hashPairInfo에 쌍 항목이 있다 = 지금 겹치는 중. CheckPair 하나가 Enter/Stay/Exit을
         그 자리에서 전부 판정하므로 "지우는 걸 깜빡한다" 류의 버그가 구조적으로 없다.
       - PreLoadCenter가 판정 전에 프레임당 한 번만 CachedCenter를 캐싱해서 CheckPair는
         그 값만 읽는다. 어떤 도형 조합인지는 IsOverlapping이 Shape을 보고 바로 호출한다.
       - Bullet 등은 Update에서 이동하므로, 그게 끝난 뒤인 LateUpdate에서 판정한다.
 *///////////////////////////////////////////

// Bullet/Missile/Guided MoveManager가 각자 LateUpdate에서 이동 Job을 끝내는 것보다
// 뒤에 돌아야 최신 위치로 판정하므로, 기본 실행 순서보다 뒤로 고정해둔다
[DefaultExecutionOrder(1000)]
public class ColliderManager : MonoBehaviour
{
    public static ColliderManager m_Instance = null;

    private const int LAYER_COUNT = 32;
    private const int JOB_BATCH_SIZE = 64;
    private const int INITIAL_BOX_CAPACITY = 128;
    private const int INITIAL_OTHER_CAPACITY = 1024;

    // m_arrLayerCollisionMatrix[i] = 레이어 i가 충돌할 레이어 마스크. 한쪽만 등록해도
    // 인식됨(IsLayerCollider 참고), Unity Physics 매트릭스와 동일한 개념
    [SerializeField] private LayerMask[] m_arrLayerCollisionMatrix = new LayerMask[LAYER_COUNT];

    // Box 콜라이더 그리드 컬링 기준점. 비워두면 컬링 없이 전부 판정 대상(안전한 기본값)
    [SerializeField] private Transform m_refPlayer;
    // 플레이어의 총알이 도달할 수 있는 최대 거리 - 이보다 먼 Box 콜라이더는 그리드에서 제외됨
    [SerializeField] private float m_fMaxBulletRange = 1000f;

    // 레이어(0~31) -> 그 레이어에 속한 활성 콜라이더 목록. Circle/Obb 공용 -
    // 레이어 하나엔 한 가지 도형만 들어간다는 게 전제(§IsOverlapping 참고)
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

    // LateUpdate 구간별(센터 캐싱/그리드 갱신/레이어 대조) 비용을 나눠보기 위한 마커 -
    // 전부 레이어 단위로만 불려서 마커 오버헤드가 측정을 왜곡할 걱정은 없음
    private static readonly ProfilerMarker s_tMarkerRefreshCenter = new ProfilerMarker("ColliderManager.PreLoadCenter");
    private static readonly ProfilerMarker s_tMarkerSameLayer = new ProfilerMarker("ColliderManager.CheckSameLayer");
    private static readonly ProfilerMarker s_tMarkerCrossLayer = new ProfilerMarker("ColliderManager.CheckCrossLayer");
    private static readonly ProfilerMarker s_tMarkerBoxGridUpdate = new ProfilerMarker("ColliderManager.UpdateBoxGrid");

    // 기존 CheckCrossLayerGrid(메인스레드 27칸 순회) 자리를 대체하는 3구간 -
    // 워커 스레드 비용은 프레임 타임에 안 드러나므로, GridJobComplete(대기 시간)가
    // 실제 체감 이득을, GridJobSchedule(SoA 복사)이 새로 생긴 비용을 말해준다
    private static readonly ProfilerMarker s_tMarkerGridSchedule = new ProfilerMarker("ColliderManager.GridJobSchedule");
    private static readonly ProfilerMarker s_tMarkerGridComplete = new ProfilerMarker("ColliderManager.GridJobComplete");
    private static readonly ProfilerMarker s_tMarkerGridDrain = new ProfilerMarker("ColliderManager.GridJobDrain");

    // 레이어(0~31) -> Shape==Box 콜라이더 그룹 전용 공간 그리드. Box가 아닌 레이어는 빈 채로 둠
    private BoxColliderGrid[] m_arrBoxGrid;

    // --- Job 입력용 SoA ---

    // Box쪽. 모든 Box 레이어가 이 한 벌을 나눠 쓰고, 레이어별 구간은 오프셋/개수로 기록한다
    // (그리드의 CellItems에는 레이어 안에서의 dense 인덱스가 들어가므로 Job이 오프셋을 더한다)
    private NativeArray<Vector3> m_arrBoxCenter;
    private NativeArray<Vector3> m_arrBoxAxisX;
    private NativeArray<Vector3> m_arrBoxAxisY;
    private NativeArray<Vector3> m_arrBoxAxisZ;
    private NativeArray<Vector3> m_arrBoxHalfExtent;
    private NativeArray<float> m_arrBoxBoundingRadius;
    private NativeArray<int> m_arrBoxColliderId;
    private int m_iBoxCount;
    private int[] m_arrBoxSoaOffset;
    private int[] m_arrBoxSoaCount;

    // 반대쪽(Circle). 그리드 하나에 대해 여러 레이어의 Circle을 한 벌로 합쳐서 한 번에 스케줄한다
    private NativeArray<Vector3> m_arrOtherCenter;
    private NativeArray<float> m_arrOtherRadius;
    private NativeArray<int> m_arrOtherColliderId;
    // "이 Circle이 지금 뭐라도 겹쳐있다고 기록돼 있는가" - 안 겹친 결과를 굳이 큐에 담을지
    // 결정하는 필터. 기록이 없으면 Exit이 나올 수 없으므로 안 담아도 결과가 완전히 동일하다
    // (기존 CheckPair 맨 앞의 m_listOther[...].Count == 0 얼리아웃과 같은 논리)
    private NativeArray<bool> m_arrOtherHasPair;
    private int m_iOtherCount;

    // Job 결과. NativeList.ParallelWriter(AddNoResize)와 달리 사전 용량 예약이 필요 없어서,
    // 최악의 프레임에도 결과가 조용히 유실될 위험이 없다
    private NativeQueue<tPairResult> m_queResult;

    private JobHandle m_tJobHandle;
    private bool m_bScheduled;

    // Job이 도는 동안 메인스레드가 처리할 "그리드를 안 쓰는" 레이어 쌍 목록
    private List<tLayerPair> m_listPendingLayerPair;
    // Box 레이어 -> 그 그리드와 대조해야 할 반대쪽(Circle) 레이어 목록
    private List<int>[] m_arrGridOtherLayer;

    private struct tColliderPair
    {
        public BaseCollider ColliderA;
        public BaseCollider ColliderB;
    }

    private struct tLayerPair
    {
        public int LayerA;
        public int LayerB;
    }

    // Job이 돌려주는 결과 한 건. Overlap=false 항목은 "후보였는데 안 겹쳤다"는 뜻으로,
    // 저번 프레임까지 겹쳐있던 쌍의 Exit을 놓치지 않기 위해 필요하다
    private struct tPairResult
    {
        public int CircleId;
        public int BoxId;
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
        m_arrBoxGrid = new BoxColliderGrid[LAYER_COUNT];
        m_arrGridOtherLayer = new List<int>[LAYER_COUNT];
        for (int i = 0; i < LAYER_COUNT; ++i)
        {
            m_arrCollider[i] = new List<BaseCollider>();
            m_arrBoxGrid[i] = new BoxColliderGrid();
            m_arrGridOtherLayer[i] = new List<int>();
        }

        m_listPendingDelete = new List<BaseCollider>();
        m_listIndexInLayerList = new List<int>();
        m_listColliderByID = new List<BaseCollider>();
        m_listOther = new List<HashSet<int>>();
        m_listPendingLayerPair = new List<tLayerPair>();

        m_hashPairInfo = new Dictionary<long, tColliderPair>();

        m_arrBoxSoaOffset = new int[LAYER_COUNT];
        m_arrBoxSoaCount = new int[LAYER_COUNT];

        AllocateBoxSoa(INITIAL_BOX_CAPACITY);
        AllocateOtherSoa(INITIAL_OTHER_CAPACITY);
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

        DisposeBoxSoa();
        DisposeOtherSoa();

        if (m_queResult.IsCreated)
            m_queResult.Dispose();

        if (m_arrBoxGrid != null)
        {
            for (int i = 0; i < LAYER_COUNT; ++i)
            {
                if (m_arrBoxGrid[i] != null)
                    m_arrBoxGrid[i].Dispose();
            }
        }
    }

    private static long MakePairKey(int _iA, int _iB)
    {
        int iLow = _iA < _iB ? _iA : _iB;
        int iHigh = _iA < _iB ? _iB : _iA;
        return ((long)iLow << 32) | (uint)iHigh;
    }

    // 레이어 i가 레이어 j와 충돌하는지. Unity Physics 매트릭스처럼 어느 한쪽 방향만 등록해도 인식됨
    private bool IsLayerCollider(int _iLayerA, int _iLayerB)
    {
        bool bAToB = (m_arrLayerCollisionMatrix[_iLayerA].value & (1 << _iLayerB)) != 0;
        bool bBToA = (m_arrLayerCollisionMatrix[_iLayerB].value & (1 << _iLayerA)) != 0;
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

        // Box 그리드는 매 프레임 통째로 재구성되므로(UpdateBoxGrids) 여기서 따로 지울 게 없다 -
        // 다음 재구성 때 이 콜라이더가 레이어 리스트에 없어서 자연히 빠진다

        List<BaseCollider> listLayer = m_arrCollider[_refCollider.Layer];
        int iLastIndex = listLayer.Count - 1;

        BaseCollider refMoved = listLayer[iLastIndex];
        listLayer[iMyIndex] = refMoved;
        listLayer.RemoveAt(iLastIndex);
        m_listIndexInLayerList[refMoved.ID] = iMyIndex;
        m_listIndexInLayerList[iID] = -1;
    }

    private void Update()
    {
        // Update()가 LateUpdate(판정)보다 항상 먼저 도니, 여기서 다 지우면 이번 프레임
        // CheckOverlaps 순회 중엔 리스트가 안 흔들림
        for (int i = 0; i < m_listPendingDelete.Count; ++i)
        {
            BaseCollider refCollider = m_listPendingDelete[i];

            // 예약 이후 재사용으로 다시 활성화됐으면 지우면 안 됨 - 실제로 비활성인 것만 삭제
            if (refCollider.gameObject.activeInHierarchy == false)
                DeleteCollider(refCollider);
        }

        m_listPendingDelete.Clear();
    }

    private void LateUpdate()
    {
        CheckOverlaps();
    }

    // 수집 -> 스케줄 -> (Job이 도는 동안) 메인스레드 작업 -> Complete -> 드레인.
    // Box 레이어가 둘 이상이면(현재 매트릭스엔 없음) 두 번째부터는 SoA/큐 버퍼를 한 벌만
    // 쓰는 구조라 겹쳐 돌리지 않고 순차적으로 스케줄->Complete->드레인을 반복한다
    private void CheckOverlaps()
    {
        PreLoadCenter();
        UpdateBoxGrids();

        CollectLayerWork();

        int iScheduledLayer = ScheduleGridJob(0);

        RunPendingLayerWork();

        while (iScheduledLayer >= 0)
        {
            CompleteAndDrainGridJob();
            iScheduledLayer = ScheduleGridJob(iScheduledLayer + 1);
        }
    }

    // Shape==Box 레이어만 대상. 그리드는 레이어당 한 번만 지어지고("정적 분할",
    // BoxColliderGrid.Build) 그 뒤로 매 프레임 셀 소속을 통째로 다시 계산한다.
    // 사거리 밖(컬링) Box는 그냥 이번 프레임 AddItem/SoA 대상에서 빠진다.
    // 기존에 있던 "Static이고 이미 그리드에 있으면 스킵" 최적화는 없어졌다 - 전체
    // 재구성 구조에선 성립하지 않고, 장애물 100개 규모라 비용도 무시할 수준
    private void UpdateBoxGrids()
    {
        using (s_tMarkerBoxGridUpdate.Auto())
        {
            bool bCullByRange = m_refPlayer != null;
            Vector3 vPlayerPos = bCullByRange ? m_refPlayer.position : Vector3.zero;
            float fMaxRangeSq = m_fMaxBulletRange * m_fMaxBulletRange;

            // 레이어별로 쓰다가 중간에 배열을 키우면 앞 레이어가 써둔 내용이 날아가므로
            // (성장 = Dispose 후 재할당, 복사 없음) 전체 상한을 먼저 구해 한 번에 확보한다
            int iTotalBox = 0;
            for (int iLayer = 0; iLayer < LAYER_COUNT; ++iLayer)
            {
                m_arrBoxSoaOffset[iLayer] = 0;
                m_arrBoxSoaCount[iLayer] = 0;

                List<BaseCollider> listLayer = m_arrCollider[iLayer];
                if (listLayer.Count == 0 || listLayer[0].Shape != eColliderShape.Box)
                    continue;

                iTotalBox += listLayer.Count;
            }

            m_iBoxCount = 0;
            if (iTotalBox == 0)
                return;

            EnsureBoxCapacity(iTotalBox);

            for (int iLayer = 0; iLayer < LAYER_COUNT; ++iLayer)
            {
                List<BaseCollider> listLayer = m_arrCollider[iLayer];
                if (listLayer.Count == 0 || listLayer[0].Shape != eColliderShape.Box)
                    continue;

                BoxColliderGrid refGrid = m_arrBoxGrid[iLayer];
                if (refGrid.IsBuilt == false)
                    refGrid.Build(listLayer);
                if (refGrid.IsBuilt == false)
                    continue;

                refGrid.BeginRebuild(listLayer.Count);

                int iOffset = m_iBoxCount;
                int iDenseIndex = 0;

                for (int i = 0; i < listLayer.Count; ++i)
                {
                    ObbCollider refBox = listLayer[i] as ObbCollider;
                    if (refBox == null)
                        continue;

                    Vector3 vCenter = refBox.CachedCenter;

                    if (bCullByRange && (vCenter - vPlayerPos).sqrMagnitude > fMaxRangeSq)
                        continue;

                    int iSoaIndex = iOffset + iDenseIndex;
                    m_arrBoxCenter[iSoaIndex] = vCenter;
                    m_arrBoxAxisX[iSoaIndex] = refBox.AxisX;
                    m_arrBoxAxisY[iSoaIndex] = refBox.AxisY;
                    m_arrBoxAxisZ[iSoaIndex] = refBox.AxisZ;
                    m_arrBoxHalfExtent[iSoaIndex] = refBox.HalfExtent;
                    m_arrBoxBoundingRadius[iSoaIndex] = refBox.BoundingRadius;
                    m_arrBoxColliderId[iSoaIndex] = refBox.ID;

                    refGrid.AddItem(iDenseIndex, vCenter);
                    ++iDenseIndex;
                }

                refGrid.EndRebuild();

                m_arrBoxSoaOffset[iLayer] = iOffset;
                m_arrBoxSoaCount[iLayer] = iDenseIndex;
                m_iBoxCount += iDenseIndex;
            }
        }
    }

    // 레이어 0~31 이중 순회(A<=B) - 여기선 아무 겹침 판정도 하지 않고 작업만 분류한다.
    // Box가 낀 쌍은 그리드별 대기 목록으로, 나머지는 메인스레드 처리 목록으로
    private void CollectLayerWork()
    {
        m_listPendingLayerPair.Clear();
        for (int i = 0; i < LAYER_COUNT; ++i)
            m_arrGridOtherLayer[i].Clear();

        for (int iLayerA = 0; iLayerA < LAYER_COUNT; ++iLayerA)
        {
            List<BaseCollider> listA = m_arrCollider[iLayerA];
            if (listA.Count == 0)
                continue;

            for (int iLayerB = iLayerA; iLayerB < LAYER_COUNT; ++iLayerB)
            {
                if (!IsLayerCollider(iLayerA, iLayerB))
                    continue;

                List<BaseCollider> listB = m_arrCollider[iLayerB];
                if (listB.Count == 0)
                    continue;

                if (iLayerA == iLayerB)
                {
                    m_listPendingLayerPair.Add(new tLayerPair { LayerA = iLayerA, LayerB = iLayerB });
                    continue;
                }

                bool bBoxA = listA[0].Shape == eColliderShape.Box;
                bool bBoxB = listB[0].Shape == eColliderShape.Box;

                // Box-Box 교차는 판정 함수(IsBoxBoxOverlap)가 항상 false라 결과가 없다 -
                // 순회 자체를 생략해도 기존과 동작이 같음(매트릭스 설정 실수 대비 안전장치)
                if (bBoxA && bBoxB)
                    continue;

                if (bBoxA)
                    m_arrGridOtherLayer[iLayerA].Add(iLayerB);
                else if (bBoxB)
                    m_arrGridOtherLayer[iLayerB].Add(iLayerA);
                else
                    m_listPendingLayerPair.Add(new tLayerPair { LayerA = iLayerA, LayerB = iLayerB });
            }
        }
    }

    // _iStartLayer 이후로 대기 목록이 있는 첫 Box 레이어를 찾아 Job 하나로 스케줄한다.
    // 같은 그리드를 쓰는 여러 레이어 쌍((Player,Obstacle)/(Obstacle,PlayerAttack)/
    // (Obstacle,MonsterAttack))을 Other SoA 한 벌로 합쳐서 스케줄 오버헤드를 한 번만 낸다 -
    // 겹침 판정도 이후 CheckPair 처리도 어느 레이어에서 왔는지와 무관하게 ID로만 동작하므로 안전.
    // 반환값: 스케줄한 Box 레이어 인덱스(없으면 -1)
    private int ScheduleGridJob(int _iStartLayer)
    {
        using (s_tMarkerGridSchedule.Auto())
        {
            for (int iLayer = _iStartLayer; iLayer < LAYER_COUNT; ++iLayer)
            {
                if (m_arrGridOtherLayer[iLayer].Count == 0)
                    continue;
                if (m_arrBoxSoaCount[iLayer] == 0)
                    continue;

                BoxColliderGrid refGrid = m_arrBoxGrid[iLayer];
                if (refGrid.IsBuilt == false)
                    continue;

                List<int> listOtherLayer = m_arrGridOtherLayer[iLayer];

                int iTotalOther = 0;
                for (int i = 0; i < listOtherLayer.Count; ++i)
                    iTotalOther += m_arrCollider[listOtherLayer[i]].Count;

                if (iTotalOther == 0)
                    continue;

                EnsureOtherCapacity(iTotalOther);

                m_iOtherCount = 0;
                for (int i = 0; i < listOtherLayer.Count; ++i)
                {
                    List<BaseCollider> listOther = m_arrCollider[listOtherLayer[i]];
                    for (int j = 0; j < listOther.Count; ++j)
                    {
                        CircleCollider refCircle = listOther[j] as CircleCollider;
                        if (refCircle == null)
                            continue;

                        m_arrOtherCenter[m_iOtherCount] = refCircle.CachedCenter;
                        m_arrOtherRadius[m_iOtherCount] = refCircle.Radius;
                        m_arrOtherColliderId[m_iOtherCount] = refCircle.ID;
                        m_arrOtherHasPair[m_iOtherCount] = m_listOther[refCircle.ID].Count > 0;
                        ++m_iOtherCount;
                    }
                }

                if (m_iOtherCount == 0)
                    continue;

                CircleBoxOverlapJob tJob = new CircleBoxOverlapJob
                {
                    CellStart = refGrid.CellStart,
                    CellCount = refGrid.CellCount,
                    CellItems = refGrid.CellItems,
                    GridOrigin = refGrid.Origin,
                    CellSize = refGrid.CellSize,
                    CountX = refGrid.CountX,
                    CountY = refGrid.CountY,
                    CountZ = refGrid.CountZ,
                    BoxOffset = m_arrBoxSoaOffset[iLayer],

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

                // 워커 스레드가 Complete까지 기다리지 않고 지금 바로 집어가게 한다 -
                // 이게 없으면 "메인스레드가 다른 일 하는 동안 겹쳐 돈다"는 전제가 깨진다
                JobHandle.ScheduleBatchedJobs();
                return iLayer;
            }
        }

        return -1;
    }

    // Job이 워커에서 도는 동안 메인스레드가 처리하는 몫(Circle-Circle 브루트포스 등)
    private void RunPendingLayerWork()
    {
        for (int i = 0; i < m_listPendingLayerPair.Count; ++i)
        {
            tLayerPair tPair = m_listPendingLayerPair[i];

            if (tPair.LayerA == tPair.LayerB)
                CheckSameLayer(m_arrCollider[tPair.LayerA]);
            else
                CheckCrossLayer(m_arrCollider[tPair.LayerA], m_arrCollider[tPair.LayerB]);
        }
    }

    // Job 결과를 실제 BaseCollider로 되돌려 Enter/Stay/Exit을 발화시키는 유일한 지점.
    // CheckPair 인자 순서는 기존(레이어 매트릭스 A<=B 순회)과 똑같이 낮은 레이어가 A -
    // 콜백 발화 순서(A->B)가 바뀌지 않게 하기 위함
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
                BaseCollider refBox = GetColliderByID(tResult.BoxId);
                BaseCollider refCircle = GetColliderByID(tResult.CircleId);

                if (refBox == null || refCircle == null)
                    continue;

                if (refBox.Layer < refCircle.Layer)
                    CheckPair(refBox, refCircle, tResult.Overlap);
                else
                    CheckPair(refCircle, refBox, tResult.Overlap);
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

    private void CheckSameLayer(List<BaseCollider> _listCollider)
    {
        using (s_tMarkerSameLayer.Auto())
        {
            for (int a = 0; a < _listCollider.Count; ++a)
            {
                BaseCollider refI = _listCollider[a];

                for (int b = a + 1; b < _listCollider.Count; ++b)
                    CheckPair(refI, _listCollider[b]);
            }
        }
    }

    private void CheckCrossLayer(List<BaseCollider> _listA, List<BaseCollider> _listB)
    {
        using (s_tMarkerCrossLayer.Auto())
        {
            for (int a = 0; a < _listA.Count; ++a)
            {
                BaseCollider refA = _listA[a];

                for (int b = 0; b < _listB.Count; ++b)
                    CheckPair(refA, _listB[b]);
            }
        }
    }

    private void CheckPair(BaseCollider _refA, BaseCollider _refB)
    {
        CheckPair(_refA, _refB, IsOverlapping(_refA, _refB));
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


    // ---- 도형별 겹침 판정 ----

    // 두 콜라이더의 Shape을 직접 보고 맞는 판정 함수를 바로 호출한다(델리게이트 테이블 없음)
    private static bool IsOverlapping(BaseCollider _refA, BaseCollider _refB)
    {
        if (_refA.Shape == eColliderShape.Circle && _refB.Shape == eColliderShape.Circle)
            return IsCircleCircleOverlap(_refA, _refB);
        if (_refA.Shape == eColliderShape.Circle && _refB.Shape == eColliderShape.Box)
            return IsCircleBoxOverlapPair(_refA, _refB);
        if (_refA.Shape == eColliderShape.Box && _refB.Shape == eColliderShape.Circle)
            return IsBoxCircleOverlapPair(_refA, _refB);

        return IsBoxBoxOverlap(_refA, _refB);
    }

    // 원-원(구-구) 판정. 3D 전체 거리(X/Y/Z)로 겹침 판정
    private static bool IsCircleCircleOverlap(BaseCollider _refA, BaseCollider _refB)
    {
        CircleCollider refCircleA = (CircleCollider)_refA;
        CircleCollider refCircleB = (CircleCollider)_refB;

        Vector3 vDelta = refCircleB.CachedCenter - refCircleA.CachedCenter;
        float fDistSq = vDelta.sqrMagnitude;
        float fRadiusSum = refCircleA.Radius + refCircleB.Radius;
        return fDistSq <= fRadiusSum * fRadiusSum;
    }

    // 원(구)-OBB 판정. 구 중심을 박스의 로컬 축 3개에 투영 -> half-extent로 클램프 ->
    // 델타 제곱합을 반지름 제곱과 비교. 씬 없이 EditMode 테스트로 검증 가능하도록 public static.
    // CircleBoxOverlapJob이 Burst로 컴파일해 그대로 호출하기도 한다(관리형 참조/예외 없음)
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

    // IsOverlapping이 Shape==(Circle,Box)일 때만 호출 - _refA는 항상 CircleCollider, _refB는 항상 ObbCollider
    private static bool IsCircleBoxOverlapPair(BaseCollider _refA, BaseCollider _refB)
    {
        CircleCollider refCircle = (CircleCollider)_refA;
        ObbCollider refBox = (ObbCollider)_refB;

        // 구-구 선판정으로 먼저 거른다 - BoundingRadius는 넉넉한 상한이라, 통과 못 하면
        // 진짜 OBB 판정(내적 3회+클램프) 없이도 100% 안 겹침(오탐/누락 없음)
        float fBoundSum = refCircle.Radius + refBox.BoundingRadius;
        if ((refCircle.CachedCenter - refBox.CachedCenter).sqrMagnitude > fBoundSum * fBoundSum)
            return false;

        return IsCircleBoxOverlap(
            refCircle.CachedCenter, refCircle.Radius,
            refBox.CachedCenter, refBox.AxisX, refBox.AxisY, refBox.AxisZ, refBox.HalfExtent);
    }

    // IsOverlapping이 Shape==(Box,Circle)일 때만 호출 - 인자만 바꿔 위 함수를 그대로 재사용
    private static bool IsBoxCircleOverlapPair(BaseCollider _refA, BaseCollider _refB)
    {
        return IsCircleBoxOverlapPair(_refB, _refA);
    }

    // 지금 매트릭스상 절대 호출 안 됨(Obstacle은 자기 자신과 비충돌) - 설정 실수 대비 안전 스텁.
    // 운석끼리 판정이 필요해지면 여기에 OBB-OBB(SAT 등) 구현
    private static bool IsBoxBoxOverlap(BaseCollider _refA, BaseCollider _refB)
    {
        return false;
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
    // 호출 시점은 반드시 "직전 프레임 Job이 Complete된 뒤"여야 한다(CheckOverlaps 참고)
    private void EnsureBoxCapacity(int _iCount)
    {
        if (m_arrBoxCenter.IsCreated && m_arrBoxCenter.Length >= _iCount)
            return;

        int iNewCapacity = m_arrBoxCenter.IsCreated ? m_arrBoxCenter.Length : INITIAL_BOX_CAPACITY;
        while (iNewCapacity < _iCount)
            iNewCapacity <<= 1;

        DisposeBoxSoa();
        AllocateBoxSoa(iNewCapacity);
    }

    private void EnsureOtherCapacity(int _iCount)
    {
        if (m_arrOtherCenter.IsCreated && m_arrOtherCenter.Length >= _iCount)
            return;

        int iNewCapacity = m_arrOtherCenter.IsCreated ? m_arrOtherCenter.Length : INITIAL_OTHER_CAPACITY;
        while (iNewCapacity < _iCount)
            iNewCapacity <<= 1;

        DisposeOtherSoa();
        AllocateOtherSoa(iNewCapacity);
    }

    private void AllocateBoxSoa(int _iCapacity)
    {
        m_arrBoxCenter = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        m_arrBoxAxisX = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        m_arrBoxAxisY = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        m_arrBoxAxisZ = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        m_arrBoxHalfExtent = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        m_arrBoxBoundingRadius = new NativeArray<float>(_iCapacity, Allocator.Persistent);
        m_arrBoxColliderId = new NativeArray<int>(_iCapacity, Allocator.Persistent);
    }

    private void AllocateOtherSoa(int _iCapacity)
    {
        m_arrOtherCenter = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        m_arrOtherRadius = new NativeArray<float>(_iCapacity, Allocator.Persistent);
        m_arrOtherColliderId = new NativeArray<int>(_iCapacity, Allocator.Persistent);
        m_arrOtherHasPair = new NativeArray<bool>(_iCapacity, Allocator.Persistent);
    }

    private void DisposeBoxSoa()
    {
        if (m_arrBoxCenter.IsCreated)
            m_arrBoxCenter.Dispose();
        if (m_arrBoxAxisX.IsCreated)
            m_arrBoxAxisX.Dispose();
        if (m_arrBoxAxisY.IsCreated)
            m_arrBoxAxisY.Dispose();
        if (m_arrBoxAxisZ.IsCreated)
            m_arrBoxAxisZ.Dispose();
        if (m_arrBoxHalfExtent.IsCreated)
            m_arrBoxHalfExtent.Dispose();
        if (m_arrBoxBoundingRadius.IsCreated)
            m_arrBoxBoundingRadius.Dispose();
        if (m_arrBoxColliderId.IsCreated)
            m_arrBoxColliderId.Dispose();
    }

    private void DisposeOtherSoa()
    {
        if (m_arrOtherCenter.IsCreated)
            m_arrOtherCenter.Dispose();
        if (m_arrOtherRadius.IsCreated)
            m_arrOtherRadius.Dispose();
        if (m_arrOtherColliderId.IsCreated)
            m_arrOtherColliderId.Dispose();
        if (m_arrOtherHasPair.IsCreated)
            m_arrOtherHasPair.Dispose();
    }

    // ---- Job ----

    // NativeArray만 참조하는 순수 struct - class/List 등 관리 객체는 여기 들어올 수 없다.
    // 반대쪽(Circle) 하나당 자기 셀 기준 이웃 27칸의 Box 후보만 검사한다(기존
    // CheckCrossLayerGrid + IsCircleBoxOverlapPair의 로직을 그대로 옮긴 것).
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
        // CellItems에는 레이어 안에서의 dense 인덱스가 들어있고, Box SoA는 모든 Box
        // 레이어가 공유하므로 이 오프셋을 더해야 실제 SoA 인덱스가 된다
        public int BoxOffset;

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

            BoxColliderGrid.ComputeCellCoord(vCenter, GridOrigin, CellSize, CountX, CountY, CountZ,
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
                            int iBox = BoxOffset + CellItems[k];

                            Vector3 vBoxCenter = BoxCenter[iBox];

                            // 구-구 선판정으로 먼저 거른다 - BoundingRadius는 넉넉한 상한이라,
                            // 통과 못 하면 진짜 OBB 판정 없이도 100% 안 겹침(오탐/누락 없음)
                            float fBoundSum = fRadius + BoxBoundingRadius[iBox];
                            Vector3 vDelta = vCenter - vBoxCenter;

                            bool bOverlap = false;
                            if (vDelta.sqrMagnitude <= fBoundSum * fBoundSum)
                            {
                                bOverlap = ColliderManager.IsCircleBoxOverlap(
                                    vCenter, fRadius,
                                    vBoxCenter, BoxAxisX[iBox], BoxAxisY[iBox], BoxAxisZ[iBox], BoxHalfExtent[iBox]);
                            }

                            // 안 겹친 결과는 "저번 프레임까지 겹쳐있던 쌍"의 Exit 판정에만 필요하다 -
                            // 이 Circle에 겹침 기록 자체가 없으면 Exit이 나올 수 없으므로 큐에 안 담는다
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
