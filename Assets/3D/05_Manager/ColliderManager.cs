using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

/*///////////////////////////////////////////
              ColliderManager
목적 : BaseCollider(Circle/Obb)들을 PhysX 없이 자체적으로 충돌 판정한다.

       - 레이어 0~31 각각에 대응하는 List<BaseCollider>(그 레이어에 속한 활성 콜라이더)를
         Awake 시점에 전부 미리 만들어둔다. 레이어 종류는 32개로 고정돼 있으니 통째로 미리
         만들어도 비용이 전혀 없음.
       - 어떤 레이어끼리 충돌할지는 개별 콜라이더가 아니라 이 매니저가 중앙에서 결정한다
         (Unity 프로젝트 세팅의 Physics 레이어 충돌 매트릭스와 동일한 개념).
       - Circle-Circle 레이어 쌍(총알-몬스터 등)은 공간 그리드를 쓰지 않는다. 총알처럼 개체 수가
         많은 쪽과 몬스터처럼 개체 수가 적은 쪽이 충돌하는 게임 특성상, 레이어 리스트끼리 전부
         대조하는 게 그리드보다 더 빠름. 반면 Shape==Box가 낀 교차-레이어 쌍(장애물 등)은
         BoxColliderGrid(정적 분할, Dictionary 없는 배열+연결리스트 기반)로 브로드페이즈한다
         - Box 콜라이더 수가 늘면서 N×M 완전 대조 비용이 무시 못 할 수준이 됐기 때문
         (§BoxColliderGrid 통합 참고).
       - m_refPlayer가 지정돼 있으면, 플레이어로부터 m_fMaxBulletRange보다 먼 Box 콜라이더는
         그리드에서 아예 빠진다(UpdateBoxGrids 참고) - 총알이 도달할 수 없는 거리의 장애물은
         판정 후보에 낄 필요조차 없기 때문. m_refPlayer가 비어있으면 컬링 없이 전부 대상.
       - m_hashPairInfo에 "쌍(A,B) 항목이 존재한다" = "지금 겹치고 있다"는 뜻으로 통일했다.
         CheckPair 하나가 Enter/Stay/Exit을 전부 그 자리에서 직접 처리한다: 안 겹치는데
         Dictionary에 남아있으면 그게 바로 이번 프레임에 떨어진 것이므로 즉시 Exit, 겹치는데
         없으면 Enter, 겹치는데 있으면 Stay. 별도의 "이번 프레임에 재확인됐는지" 집합이나
         프레임 끝 별도 패스가 없어서 "지우는 걸 깜빡한다" 종류의 버그 자체가 성립 안 함
         (다만 안 겹치는 후보 쌍도 항상 Dictionary 조회 한 번은 하게 됨 - 대부분 안 겹치는
         총알-몬스터 후보 쌍 특성상 트레이드오프임).
       - 콜라이더별로 "현재 관여 중인 쌍" ID 목록(m_listOther)을 별도로 들고 있다가,
         UnActivate 시 그 쌍 기록들을 즉시 정리한다(겹친 채로 반납되면 Exit도 쏴줌). Circle/Obb
         구분 없이 ID 하나로 통합돼 있어서 원-원/원-박스/박스-박스 쌍이 전부 이 하나의 정리
         로직으로 처리된다.
       - BaseCollider.Center는 도형별로 계산 비용이 다르다(Circle/Obb 둘 다 쿼터니언 곱셈 포함,
         Obb는 축도 매 프레임 갱신). 판정 루프 돌기 전에 PreLoadCenter로 활성 콜라이더마다
         프레임당 딱 한 번만 다형 호출해서 캐시해두고, CheckPair는 CachedCenter만 읽는다.
       - 어떤 도형끼리(원-원/원-박스/박스-박스) 겹침을 계산할지는 CheckPair가 두 콜라이더의
         Shape을 직접 비교해서 그때그때 맞는 판정 함수를 바로 호출한다(델리게이트 테이블 없음).
       - Bullet 등은 Update에서 Transform을 직접 이동하므로, 그게 전부 끝나 위치가 확정된
         뒤인 LateUpdate에서 판정한다.
       - 레이어 리스트에서의 실제 제거(스왑백)는 판정 순회 중에는 절대 안 하고 다음 프레임
         Update() 맨 앞에서 한꺼번에 처리한다 (m_listPendingDelete 주석 참고).
 *///////////////////////////////////////////

// BulletMoveManager/MissileMoveManager/GuidedMoveManager는 각자 LateUpdate에서 Job을
// Complete()하고 결과를 Transform에 적용한다. 이 판정은 그 이후, 즉 이번 프레임 최신
// 위치가 전부 반영된 뒤에 돌아야 하므로 기본 실행 순서보다 뒤로 고정해둔다
[DefaultExecutionOrder(1000)]
public class ColliderManager : MonoBehaviour
{
    public static ColliderManager m_Instance = null;

    // 레이어별 충돌 매트릭스. m_arrLayerCollisionMatrix[i] = 레이어 i가 충돌할 레이어들의 마스크.
    // Unity Physics 설정처럼 한쪽만 체크해도 인식되도록 양방향으로 확인함(IsLayerCollide 참고).
    [SerializeField] private LayerMask[] m_arrLayerCollisionMatrix = new LayerMask[32];

    // Box 콜라이더 그리드 컬링 기준점. 비워두면 컬링 없이 전부 판정 대상(안전한 기본값)
    [SerializeField] private Transform m_refPlayer;
    // 플레이어의 총알이 도달할 수 있는 최대 거리 - 이보다 먼 Box 콜라이더는 그리드에서 제외됨
    [SerializeField] private float m_fMaxBulletRange = 1000f;

    // 레이어(0~31) -> 그 레이어에 속한 활성 콜라이더 목록. Awake에서 32개 전부 미리 생성.
    // Circle/Obb 공용 - 레이어 하나에는 한 가지 도형만 들어간다는 게 전제(§IsOverlapping 참고)
    private List<BaseCollider>[] m_arrCollider;

    // UnActivate로 예약된, 다음 프레임 Update() 맨 앞에서 한꺼번에 지워줄 콜라이더들.
    // 이미 맞은 얘들은 호출까지는 보장하기 위해서 LateUpdate에서 사라져도 바로 사라지지 않게. 다음 프레임에서 삭제되게
    private List<BaseCollider> m_listPendingDelete;

    // ID -> 그 콜라이더가 자기 레이어 리스트에서 몇 번째 자리인지 (UnActivate 스왑백 O(1) 제거용)
    private List<int> m_listIndexInLayerList;

    // ID -> 지금 이 ID와 실제로 겹쳐있다고 기록된 상대 ID들. UnActivate 시 관련 쌍 정리용
    private List<HashSet<int>> m_listOther;

    // 쌍(A,B) -> 항목이 존재 = 지금 겹치고 있다는 뜻 (Enter 시 생성, Exit 시 제거).
    // Enter/Stay/Exit 판정을 전부 CheckPair 안에서 이 Dictionary 하나로 직접 처리한다.
    // Circle-Circle/Circle-Box/Box-Box 쌍 전부 이 Dictionary 하나로 통합 처리(ID 공간이
    // BaseCollider에서 공유되므로 별도 Dictionary가 필요 없음)
    private Dictionary<long, tColliderPair> m_hashPairInfo;

    // Stay 처리 비용만 따로 떼서 프로파일러(CPU Usage > Hierarchy)에서 확인하기 위한 마커.
    // Deep Profile 없이도 이 이름으로 항목이 잡힘. CheckPair 자체를 감싸지 않는 이유는 프레임당
    // 수만 번 불려서 마커 오버헤드가 측정을 왜곡할 수 있기 때문 - Stay 발생 쌍만 상대적으로
    // 적어서 여기엔 감싸도 괜찮음
    private static readonly ProfilerMarker s_tMarkerStay = new ProfilerMarker("ColliderManager.OnStay");

    // ColliderManager.LateUpdate() 안에서 어느 구간이 무거운지(센터 캐싱 / 같은 레이어 대조
    // / 레이어 간 대조) 나눠서 보기 위한 마커. 이 세 개는 레이어 단위로만 불려서 프레임당
    // 호출 횟수가 적으니 마커 오버헤드로 측정이 왜곡될 걱정 없음. Circle-Box 판정도 이제
    // CheckSameLayer/CheckCrossLayer 안에 통합돼 있어서 별도 마커 없이 여기서 같이 잡힘
    private static readonly ProfilerMarker s_tMarkerRefreshCenter = new ProfilerMarker("ColliderManager.PreLoadCenter");
    private static readonly ProfilerMarker s_tMarkerSameLayer = new ProfilerMarker("ColliderManager.CheckSameLayer");
    private static readonly ProfilerMarker s_tMarkerCrossLayer = new ProfilerMarker("ColliderManager.CheckCrossLayer");
    private static readonly ProfilerMarker s_tMarkerBoxGridUpdate = new ProfilerMarker("ColliderManager.UpdateBoxGrid");
    private static readonly ProfilerMarker s_tMarkerCrossLayerGrid = new ProfilerMarker("ColliderManager.CheckCrossLayerGrid");

    // 레이어(0~31) -> Shape==Box인 콜라이더 그룹 전용 공간 그리드. Box가 아닌 레이어는
    // 그냥 비어있는 채로 둔다(RefreshBoxGrids가 Shape 확인 후에만 채움)
    private BoxColliderGrid[] m_arrBoxGrid;

    // BoxColliderGrid.NeighborColliders 결과를 담는 재사용 버퍼 - 총알 수만큼
    // 매 프레임 호출되므로 호출부(CheckCrossLayerGrid)가 매번 새 List를 만들지 않게 함
    private List<BaseCollider> m_listGridNeighbor;

    private struct tColliderPair
    {
        public BaseCollider ColliderA;
        public BaseCollider ColliderB;
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

        m_arrCollider = new List<BaseCollider>[32];
        m_arrBoxGrid = new BoxColliderGrid[32];
        for (int i = 0; i < 32; ++i)
        {
            m_arrCollider[i] = new List<BaseCollider>();
            m_arrBoxGrid[i] = new BoxColliderGrid();
        }

        m_listPendingDelete = new List<BaseCollider>();
        m_listIndexInLayerList = new List<int>();
        m_listOther = new List<HashSet<int>>();
        m_listGridNeighbor = new List<BaseCollider>();

        m_hashPairInfo = new Dictionary<long, tColliderPair>();
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
            m_listOther.Add(new HashSet<int>());
        }
    }

    // BaseCollider.Awake()에서 생애주기 중 딱 한 번만 호출. 레이어 리스트엔 아직 안 들어감(Activate가 담당)
    public void RegisterCollider(BaseCollider _refCollider)
    {
        ResizeCapacity(_refCollider.ID);
    }

    // BaseCollider.OnEnable()/Start()에서 호출 - 그 즉시 자기 레이어 리스트에 편입.
    // 죽은 직후(리스트 제거는 예약만 되고 아직 안 지워진 상태)에 바로 재발사되는 경우, 이미
    // 리스트에 남아있는데 또 추가하면 같은 콜라이더가 두 자리를 차지하는 유령 항목이 생겨서
    // m_listIndexInLayerList가 꼬이므로, 이미 등록돼 있으면(index가 -1이 아니면) 무시
    public void Activate(BaseCollider _refCollider)
    {
        ResizeCapacity(_refCollider.ID);

        if (m_listIndexInLayerList[_refCollider.ID] >= 0)
            return;

        List<BaseCollider> listLayer = m_arrCollider[_refCollider.Layer];
        m_listIndexInLayerList[_refCollider.ID] = listLayer.Count;
        listLayer.Add(_refCollider);
    }

    // BaseCollider.OnDisable()에서 호출.
    // 쌍 기록 정리(Exit 이벤트 포함)는 리스트 순회와 무관해서 안전하니 여기서 바로 처리한다.
    // 레이어 리스트에서의 실제 제거는 다음 프레임 Update() 맨 앞으로 예약만 해둔다 - 지금 당장
    // (판정 순회 도중, 총알이 맞자마자 즉발로 풀 반납되는 재진입 호출 등으로) 스왑백 제거를
    // 하면 CheckSameLayer/CheckCrossLayer가 인덱스로 순회 중인 그 리스트가 흔들려서, 방금 지운
    // 자리로 스왑되어 들어온(원래 리스트 맨 뒤에 있던) 다른 콜라이더가 이번 프레임 판정에서
    // 통째로 스킵될 수 있기 때문
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


        // 이 콜라이더가 관여하던 쌍 기록을 전부 정리 (겹친 채로 반납되면 Exit도 쏴줌).
        // m_hashPairInfo에 항목이 있다는 것 자체가 "지금 겹치는 중"이라는 뜻이라 별도 플래그 확인 불필요.
        // Circle-Circle이든 Circle-Box든 이 Dictionary 하나로 처리되므로 도형 구분 없이 동일하게 정리됨
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

        // BoxColliderGrid는 매 프레임 통째로 다시 짓지 않고 이동분만 갱신하는 구조라서,
        // 파괴/풀 반납되는 콜라이더를 여기서 명시적으로 지워주지 않으면 죽은 참조가
        // 셀에 영원히 남는다(§BoxColliderGrid 클래스 주석 참고)
        if (_refCollider.Shape == eColliderShape.Box)
            m_arrBoxGrid[_refCollider.Layer].RemoveCollider(_refCollider);

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
        // Update()는 LateUpdate(판정)보다 항상 먼저 돌므로, 여기서 다 지워두면
        // 이번 프레임 CheckOverlaps 순회 중엔 리스트가 절대 안 흔들림.
        // 예약 이후 재활성화(재발사 등)됐으면 gameObject.activeInHierarchy가 다시 true가 되어
        // 있으므로, 그 사이 다시 살아난 콜라이더까지 실수로 지우지 않도록 걸러줌
        for (int i = 0; i < m_listPendingDelete.Count; ++i)
        {
            BaseCollider refCollider = m_listPendingDelete[i];

            // 예약 이후(같은 프레임 or 다음 Update 전에) 재사용으로 다시 활성화됐으면 지우면 안 됨 -
            // 지금 실제로 비활성 상태인 것만 진짜로 삭제
            if (refCollider.gameObject.activeInHierarchy == false)
                DeleteCollider(refCollider);
        }

        m_listPendingDelete.Clear();
    }

    private void LateUpdate()
    {
        CheckOverlaps();
    }

    private void CheckOverlaps()
    {
        PreLoadCenter();
        UpdateBoxGrids();

        // 레이어 0~31을 이중 순회(A<=B), 매트릭스에서 충돌하는 조합만 실제로 대조
        for (int iLayerA = 0; iLayerA < 32; ++iLayerA)
        {
            List<BaseCollider> listA = m_arrCollider[iLayerA];
            if (listA.Count == 0)
                continue;

            for (int iLayerB = iLayerA; iLayerB < 32; ++iLayerB)
            {
                if (!IsLayerCollider(iLayerA, iLayerB))
                    continue;

                List<BaseCollider> listB = m_arrCollider[iLayerB];
                if (listB.Count == 0)
                    continue;

                if (iLayerA == iLayerB)
                    CheckSameLayer(listA);
                else if (listA[0].Shape == eColliderShape.Box)
                    CheckCrossLayerGrid(m_arrBoxGrid[iLayerA], listB, _bBoxIsA: true);
                else if (listB[0].Shape == eColliderShape.Box)
                    CheckCrossLayerGrid(m_arrBoxGrid[iLayerB], listA, _bBoxIsA: false);
                else
                    CheckCrossLayer(listA, listB);
            }
        }
    }

    // Shape==Box인 레이어만 대상. 그리드는 레이어당 딱 한 번만 지어진다(BoxColliderGrid.Build,
    // "정적 분할" - 그 시점 Box 콜라이더 분포로 범위가 정해짐). 그 뒤로는 매 프레임:
    //  - m_refPlayer 기준 사거리(m_fMaxBulletRange) 밖으로 나간 애는 그리드에서 뺀다
    //  - 사거리 안이고 Static이면서 이미 그리드에 있으면 스킵(자기 위치가 안 바뀌므로 재계산 불필요)
    //  - 그 외(사거리 안 + 새로 들어왔거나 Static이 아님)는 UpdateCell로 셀 소속을 갱신
    private void UpdateBoxGrids()
    {
        using (s_tMarkerBoxGridUpdate.Auto())
        {
            bool bCullByRange = m_refPlayer != null;
            Vector3 vPlayerPos = bCullByRange ? m_refPlayer.position : Vector3.zero;
            float fMaxRangeSq = m_fMaxBulletRange * m_fMaxBulletRange;

            for (int iLayer = 0; iLayer < 32; ++iLayer)
            {
                List<BaseCollider> listLayer = m_arrCollider[iLayer];
                if (listLayer.Count == 0 || listLayer[0].Shape != eColliderShape.Box)
                    continue;

                BoxColliderGrid refGrid = m_arrBoxGrid[iLayer];
                if (!refGrid.IsBuilt)
                    refGrid.Build(listLayer);

                for (int i = 0; i < listLayer.Count; ++i)
                {
                    BaseCollider refCollider = listLayer[i];

                    bool bInRange = bCullByRange == false
                        || (refCollider.CachedCenter - vPlayerPos).sqrMagnitude <= fMaxRangeSq;

                    if (bInRange == false)
                    {
                        refGrid.RemoveCollider(refCollider);
                        continue;
                    }

                    if (refCollider.StaticObject && refGrid.Contains(refCollider))
                        continue;

                    refGrid.UpdateCell(refCollider);
                }
            }
        }
    }

    // Box 콜라이더 레이어와 교차하는 쌍 전용 - N×M 완전 대조 대신, 반대쪽 콜라이더마다
    // 자기 셀 기준 이웃 그리드 후보만 CheckPair로 넘긴다. CheckPair 자체(Enter/Stay/Exit,
    // m_hashPairInfo, 조기 스킵)는 전혀 손대지 않고 그대로 재사용
    private void CheckCrossLayerGrid(BoxColliderGrid _refGrid, List<BaseCollider> _listOtherSide, bool _bBoxIsA)
    {
        using (s_tMarkerCrossLayerGrid.Auto())
        {
            for (int i = 0; i < _listOtherSide.Count; ++i)
            {
                BaseCollider refOther = _listOtherSide[i];
                _refGrid.NeighborColliders(refOther.CachedCenter, m_listGridNeighbor);

                for (int j = 0; j < m_listGridNeighbor.Count; ++j)
                {
                    BaseCollider refBox = m_listGridNeighbor[j];
                    if (_bBoxIsA)
                        CheckPair(refBox, refOther);
                    else
                        CheckPair(refOther, refBox);
                }
            }
        }
    }

    private void PreLoadCenter()
    {
        using (s_tMarkerRefreshCenter.Auto())
        {
            for (int iLayer = 0; iLayer < 32; ++iLayer)
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
        bool bOverlapping = IsOverlapping(_refA, _refB);

        //이번 프레임에 맞았다면
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
            //이번 프레임에 처음 맞았다면
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
            // _refA가 지금 아무와도 안 겹치는 중이면(총알처럼 개체 수가 많은 쪽은 한 순간에
            // 겹치는 상대가 0~1개뿐인 게 보통) 이 쌍도 당연히 Dictionary에 기록이 없다는
            // 뜻이므로 조회 자체를 건너뛴다 - 안 겹치는 쌍이 압도적으로 많은 구조라
            // (총알 5000 x 운석 129 ≈ 66만 쌍 중 실제 겹침은 극소수) 이 한 줄이 큰 비용을 아낀다
            if (m_listOther[_refA.ID].Count == 0)
                return;

            //저번 프레임에서도 맞았다면
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

    // 원(구)-OBB 판정 순수 함수. 구 중심을 박스의 로컬 축 3개에 투영(내적) -> half-extent로
    // 클램프 -> 델타 제곱합을 반지름 제곱과 비교. 정규직교 기저라 로컬에서 잰 거리가 곧
    // 실제 거리라 월드로 되돌릴 필요가 없음. 씬/스크립트 없이 EditMode 테스트로 직접 검증
    // 가능하도록 public static으로 노출
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

        // 구-구 선판정(broad-phase)으로 명백히 먼 쌍을 먼저 걸러낸다. BoundingRadius는
        // half-extent 대각선 길이라 실제 박스보다 넉넉한 상한이므로, 이 테스트를 통과 못 하면
        // 진짜 OBB 판정(내적 3회+클램프)을 계산할 필요도 없이 100% 안 겹침 - 반대로 이 테스트를
        // 통과했다고 꼭 겹치는 건 아니라 아래 정밀 판정으로 넘어감(오탐 없음, 누락도 없음)
        float fBoundSum = refCircle.Radius + refBox.BoundingRadius;
        if ((refCircle.CachedCenter - refBox.CachedCenter).sqrMagnitude > fBoundSum * fBoundSum)
            return false;

        return IsCircleBoxOverlap(
            refCircle.CachedCenter, refCircle.Radius,
            refBox.CachedCenter, refBox.AxisX, refBox.AxisY, refBox.AxisZ, refBox.HalfExtent);
    }

    // IsOverlapping이 Shape==(Box,Circle)일 때만 호출 - _refA는 항상 ObbCollider, _refB는 항상 CircleCollider.
    // 인자만 바꿔 위 함수를 그대로 재사용
    private static bool IsBoxCircleOverlapPair(BaseCollider _refA, BaseCollider _refB)
    {
        return IsCircleBoxOverlapPair(_refB, _refA);
    }

    // 지금 레이어 와이어링상 절대 호출되지 않음(운석끼리는 충돌 검사 안 함) - 매트릭스 설정
    // 실수로 Obstacle 레이어가 자기 자신과 충돌하도록 켜지는 경우에 대비한 안전한 스텁.
    // 실제로 운석끼리 충돌 판정이 필요해지면 여기에 OBB-OBB(SAT 등) 구현을 채울 것
    private static bool IsBoxBoxOverlap(BaseCollider _refA, BaseCollider _refB)
    {
        return false;
    }

    // Physics.Raycast 대체용 - CircleCollider(PhysX 없음) 대상으로 레이 판정. Aim 등 화면 좌표
    // 기반 조준/커서 판정에서 사용. LayerMask는 값 타입이라 할당 없이 그대로 넘길 수 있음
    // (호출부에서 원하는 레이어를 미리 비트마스크로 조합해서 넘겨줌). Circle 전용 쿼리라
    // m_arrCollider에서 CircleCollider인 것만 걸러서 본다(운석 등 Box는 대상 아님)
    public bool RaycastMask(Vector3 _vOrigin, Vector3 _vDir, float _fMaxLength, LayerMask _tMask, out CircleCollider _refHit)
    {
        _vDir.Normalize();

        CircleCollider refClosest = null;
        float fClosestT = float.MaxValue;

        for (int iLayer = 0; iLayer < 32; ++iLayer)
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

    // Physics.OverlapSphereNonAlloc 대체용 - CircleCollider(PhysX 없음) 대상으로 반경 내 전체 목록 판정.
    // 레이더 등 "화면 안 위협 하나"가 아니라 "범위 내 전부"가 필요한 곳에서 사용.
    // 호출부가 들고 있는 리스트를 재사용하도록 넘겨받아 채우는 방식이라 매 호출 할당이 없음(Clear만 함).
    // Circle 전용 쿼리라 m_arrCollider에서 CircleCollider인 것만 걸러서 본다
    public void FindAllInRadius(Vector3 _vPos, float _fRadius, LayerMask _tMask, List<CircleCollider> _listResult)
    {
        _listResult.Clear();
        float fRadiusSq = _fRadius * _fRadius;

        for (int iLayer = 0; iLayer < 32; ++iLayer)
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

    // Physics.OverlapSphere 대체용 - CircleCollider(PhysX 없음) 대상으로 반경 내 최근접 판정.
    // TargetScanner 등 위치 기반 최근접 타겟 탐색에서 사용. 3D 전체 거리로 판정.
    // Circle 전용 쿼리라 m_arrCollider에서 CircleCollider인 것만 걸러서 본다
    public bool FindNearest(Vector3 _vPos, float _fRadius, LayerMask _tMask, out CircleCollider _refHit)
    {
        CircleCollider refNearest = null;
        float fNearestDistSq = float.MaxValue;
        float fRadiusSq = _fRadius * _fRadius;

        for (int iLayer = 0; iLayer < 32; ++iLayer)
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

}
