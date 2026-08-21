using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

/*///////////////////////////////////////////
              ColliderManager
목적 : BaseCollider(Circle/Obb)들을 PhysX 없이 자체적으로 충돌 판정한다.

       - 레이어 0~31마다 List<BaseCollider>를 Awake에서 미리 만들어두고, 어떤 레이어끼리
         충돌할지는 m_arrLayerCollisionMatrix로 이 매니저가 중앙에서 결정한다.
       - Circle-Circle 쌍(총알-몬스터 등)은 브루트포스가 그리드보다 빠르므로 그대로 두고,
         Shape==Box가 낀 쌍만 BoxColliderGrid로 브로드페이즈한다. m_refPlayer가 있으면
         사거리(m_fMaxBulletRange) 밖 Box는 그리드에서 아예 빠진다(UpdateBoxGrids 참고).
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

    // m_arrLayerCollisionMatrix[i] = 레이어 i가 충돌할 레이어 마스크. 한쪽만 등록해도
    // 인식됨(IsLayerCollider 참고), Unity Physics 매트릭스와 동일한 개념
    [SerializeField] private LayerMask[] m_arrLayerCollisionMatrix = new LayerMask[32];

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
    private static readonly ProfilerMarker s_tMarkerCrossLayerGrid = new ProfilerMarker("ColliderManager.CheckCrossLayerGrid");

    // 레이어(0~31) -> Shape==Box 콜라이더 그룹 전용 공간 그리드. Box가 아닌 레이어는 빈 채로 둠
    private BoxColliderGrid[] m_arrBoxGrid;

    // BoxColliderGrid.NeighborColliders 결과 재사용 버퍼 - 총알 수만큼 매 프레임
    // 호출되므로(CheckCrossLayerGrid) 매번 새 List를 안 만들게 함
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
    // 재발사 등으로 죽은 직후 다시 활성화될 때 중복 등록(유령 항목) 방지 - 이미 등록돼 있으면 무시
    public void Activate(BaseCollider _refCollider)
    {
        ResizeCapacity(_refCollider.ID);

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

        // 그리드는 이동분만 갱신하는 구조라, 파괴/풀 반납 시 여기서 명시적으로 지우지 않으면
        // 죽은 참조가 셀에 영원히 남는다
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

    // Shape==Box 레이어만 대상. 그리드는 레이어당 한 번만 지어지고("정적 분할",
    // BoxColliderGrid.Build) 그 뒤로 매 프레임: 사거리 밖으로 나간 애는 그리드에서
    // 빼고, 사거리 안이면서 Static이고 이미 그리드에 있으면 스킵, 나머지만 UpdateCell
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
    // 자기 셀 기준 이웃 그리드 후보만 CheckPair로 넘긴다
    private void CheckCrossLayerGrid(BoxColliderGrid _refGrid, List<BaseCollider> _listOtherSide, bool _bBoxIsA)
    {
        using (s_tMarkerCrossLayerGrid.Auto())
        {
            for (int i = 0; i < _listOtherSide.Count; ++i)
            {
                BaseCollider refOther = _listOtherSide[i];
                // Box-Box 크로스 레이어일 때도 이 함수를 타지만 IsBoxBoxOverlap이 항상 false라 실질적
                // 쌍이 아니므로 반지름 0(=링 1겹)으로 둬도 무해. 나중에 OBB-OBB 판정을 구현하면 재검토할 것
                float fQueryRadius = (refOther is CircleCollider refCircleOther) ? refCircleOther.Radius : 0f;
                _refGrid.NeighborColliders(refOther.CachedCenter, fQueryRadius, m_listGridNeighbor);

                for (int j = 0; j < m_listGridNeighbor.Count; ++j)
                {
                    BaseCollider refBox = m_listGridNeighbor[j];
                    if (_bBoxIsA == true)
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

        if (bOverlapping)
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
    // 델타 제곱합을 반지름 제곱과 비교. 씬 없이 EditMode 테스트로 검증 가능하도록 public static
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

    // Physics.OverlapSphereNonAlloc 대체용(PhysX 없음) - 범위 내 전체 목록. Circle 전용,
    // 호출부 재사용 리스트를 Clear 후 채우므로 무할당
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

    // Physics.OverlapSphere 대체용(PhysX 없음) - 범위 내 최근접 하나. Circle 전용
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
