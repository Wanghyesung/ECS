using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

/*///////////////////////////////////////////
              ColliderManager
목적 : CircleCollider들을 PhysX 없이 자체적으로 원(구) 충돌 판정한다.

       - 레이어 0~31 각각에 대응하는 List<CircleCollider>(그 레이어에 속한 활성 콜라이더)를
         Awake 시점에 전부 미리 만들어둔다. 레이어 종류는 32개로 고정돼 있으니 통째로 미리
         만들어도 비용이 전혀 없음.
       - 어떤 레이어끼리 충돌할지는 개별 콜라이더가 아니라 이 매니저가 중앙에서 결정한다
         (Unity 프로젝트 세팅의 Physics 레이어 충돌 매트릭스와 동일한 개념).
       - 공간 그리드(셀 분할)는 쓰지 않는다. 총알처럼 개체 수가 많은 쪽과 몬스터처럼 개체 수가
         적은 쪽이 충돌하는 게임 특성상, 레이어 리스트끼리 전부 대조하는 게 그리드보다 더 빠름.
       - m_hashPairInfo에 "쌍(A,B) 항목이 존재한다" = "지금 겹치고 있다"는 뜻으로 통일했다.
         CheckPair 하나가 Enter/Stay/Exit을 전부 그 자리에서 직접 처리한다: 안 겹치는데
         Dictionary에 남아있으면 그게 바로 이번 프레임에 떨어진 것이므로 즉시 Exit, 겹치는데
         없으면 Enter, 겹치는데 있으면 Stay. 별도의 "이번 프레임에 재확인됐는지" 집합이나
         프레임 끝 별도 패스가 없어서 "지우는 걸 깜빡한다" 종류의 버그 자체가 성립 안 함
         (다만 안 겹치는 후보 쌍도 항상 Dictionary 조회 한 번은 하게 됨 - 대부분 안 겹치는
         총알-몬스터 후보 쌍 특성상 트레이드오프임).
       - 콜라이더별로 "현재 관여 중인 쌍" ID 목록(m_listOther)을 별도로 들고 있다가,
         UnActivate 시 그 쌍 기록들을 즉시 정리한다(겹친 채로 반납되면 Exit도 쏴줌).
       - CircleCollider.Center는 쿼터니언 곱셈이 들어있어 쌍마다 반복 조회하면 낭비가 크다
         (총알 하나가 몬스터 수만큼, 몬스터 하나가 총알 수만큼 매번 다시 계산됨). 판정 루프
         돌기 전에 RefreshAllCenters로 활성 콜라이더마다 프레임당 딱 한 번만 계산해서
         캐시해두고, CheckPair는 CachedCenter만 읽는다.
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

    // 레이어(0~31) -> 그 레이어에 속한 활성 콜라이더 목록. Awake에서 32개 전부 미리 생성
    private List<CircleCollider>[] m_arrCollider;

    // UnActivate로 예약된, 다음 프레임 Update() 맨 앞에서 한꺼번에 지워줄 콜라이더들.
    // 이미 맞은 얘들은 호출까지는 보장하기 위해서 LateUpdate에서 사라져도 바로 사라지지 않게. 다음 프레임에서 삭제되게
    private List<CircleCollider> m_listPendingDelete;

    // ID -> 그 콜라이더가 자기 레이어 리스트에서 몇 번째 자리인지 (UnActivate 스왑백 O(1) 제거용)
    private List<int> m_listIndexInLayerList;

    // ID -> 지금 이 ID와 실제로 겹쳐있다고 기록된 상대 ID들. UnActivate 시 관련 쌍 정리용
    private List<HashSet<int>> m_listOther;

    // 쌍(A,B) -> 항목이 존재 = 지금 겹치고 있다는 뜻 (Enter 시 생성, Exit 시 제거).
    // Enter/Stay/Exit 판정을 전부 CheckPair 안에서 이 Dictionary 하나로 직접 처리한다.
    private Dictionary<long, tColliderPair> m_hashPairInfo;

    // Stay 처리 비용만 따로 떼서 프로파일러(CPU Usage > Hierarchy)에서 확인하기 위한 마커.
    // Deep Profile 없이도 이 이름으로 항목이 잡힘. CheckPair 자체를 감싸지 않는 이유는 프레임당
    // 수만 번 불려서 마커 오버헤드가 측정을 왜곡할 수 있기 때문 - Stay 발생 쌍만 상대적으로
    // 적어서 여기엔 감싸도 괜찮음
    private static readonly ProfilerMarker s_tMarkerStay = new ProfilerMarker("ColliderManager.OnStay");

    // ColliderManager.LateUpdate() 안에서 어느 구간이 무거운지(센터 캐싱 / 같은 레이어 대조
    // / 레이어 간 대조) 나눠서 보기 위한 마커. 이 세 개는 레이어 단위로만 불려서 프레임당
    // 호출 횟수가 적으니 마커 오버헤드로 측정이 왜곡될 걱정 없음
    private static readonly ProfilerMarker s_tMarkerRefreshCenter = new ProfilerMarker("ColliderManager.PreLoadCenter");
    private static readonly ProfilerMarker s_tMarkerSameLayer = new ProfilerMarker("ColliderManager.CheckSameLayer");
    private static readonly ProfilerMarker s_tMarkerCrossLayer = new ProfilerMarker("ColliderManager.CheckCrossLayer");

    private struct tColliderPair
    {
        public CircleCollider ColliderA;
        public CircleCollider ColliderB;
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

        m_arrCollider = new List<CircleCollider>[32];
        for (int i = 0; i < 32; ++i)
            m_arrCollider[i] = new List<CircleCollider>();

        m_listPendingDelete = new List<CircleCollider>();
        m_listIndexInLayerList = new List<int>();
        m_listOther = new List<HashSet<int>>();

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

    // CircleCollider.Awake()에서 생애주기 중 딱 한 번만 호출. 레이어 리스트엔 아직 안 들어감(Activate가 담당)
    public void RegisterCollider(CircleCollider _refCollider)
    {
        ResizeCapacity(_refCollider.ID);
    }

    // CircleCollider.OnEnable()/Start()에서 호출 - 그 즉시 자기 레이어 리스트에 편입.
    // 죽은 직후(리스트 제거는 예약만 되고 아직 안 지워진 상태)에 바로 재발사되는 경우, 이미
    // 리스트에 남아있는데 또 추가하면 같은 콜라이더가 두 자리를 차지하는 유령 항목이 생겨서
    // m_listIndexInLayerList가 꼬이므로, 이미 등록돼 있으면(index가 -1이 아니면) 무시
    public void Activate(CircleCollider _refCollider)
    {
        ResizeCapacity(_refCollider.ID);

        if (m_listIndexInLayerList[_refCollider.ID] >= 0)
            return;

        List<CircleCollider> listLayer = m_arrCollider[_refCollider.Layer];
        m_listIndexInLayerList[_refCollider.ID] = listLayer.Count;
        listLayer.Add(_refCollider);
    }

    // CircleCollider.OnDisable()에서 호출.
    // 쌍 기록 정리(Exit 이벤트 포함)는 리스트 순회와 무관해서 안전하니 여기서 바로 처리한다.
    // 레이어 리스트에서의 실제 제거는 다음 프레임 Update() 맨 앞으로 예약만 해둔다 - 지금 당장
    // (판정 순회 도중, 총알이 맞자마자 즉발로 풀 반납되는 재진입 호출 등으로) 스왑백 제거를
    // 하면 CheckSameLayer/CheckCrossLayer가 인덱스로 순회 중인 그 리스트가 흔들려서, 방금 지운
    // 자리로 스왑되어 들어온(원래 리스트 맨 뒤에 있던) 다른 콜라이더가 이번 프레임 판정에서
    // 통째로 스킵될 수 있기 때문
    public void UnActivate(CircleCollider _refCollider)
    {
        m_listPendingDelete.Add(_refCollider);
    }

    // 예약된 콜라이더를 레이어 리스트에서 스왑백 제거, ColliderExit호출, 충돌 쌍 제거
    private void DeleteCollider(CircleCollider _refCollider)
    {
        int iID = _refCollider.ID;
        int iMyIndex = m_listIndexInLayerList[iID];
        if (iMyIndex < 0)
            return; // 이미 처리됨 (중복 예약 가드)


        // 이 콜라이더가 관여하던 쌍 기록을 전부 정리 (겹친 채로 반납되면 Exit도 쏴줌).
        // m_hashPairInfo에 항목이 있다는 것 자체가 "지금 겹치는 중"이라는 뜻이라 별도 플래그 확인 불필요
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


        List<CircleCollider> listLayer = m_arrCollider[_refCollider.Layer];
        int iLastIndex = listLayer.Count - 1;

        CircleCollider refMoved = listLayer[iLastIndex];
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
            CircleCollider refCollider = m_listPendingDelete[i];

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

        // 레이어 0~31을 이중 순회(A<=B), 매트릭스에서 충돌하는 조합만 실제로 대조
        for (int iLayerA = 0; iLayerA < 32; ++iLayerA)
        {
            List<CircleCollider> listA = m_arrCollider[iLayerA];
            if (listA.Count == 0)
                continue;

            for (int iLayerB = iLayerA; iLayerB < 32; ++iLayerB)
            {
                if (!IsLayerCollider(iLayerA, iLayerB))
                    continue;

                List<CircleCollider> listB = m_arrCollider[iLayerB];
                if (listB.Count == 0)
                    continue;

                if (iLayerA == iLayerB)
                    CheckSameLayer(listA);
                else
                    CheckCrossLayer(listA, listB);
            }
        }
    }

    private void PreLoadCenter()
    {
        using (s_tMarkerRefreshCenter.Auto())
        {
            for (int iLayer = 0; iLayer < 32; ++iLayer)
            {
                List<CircleCollider> listLayer = m_arrCollider[iLayer];
                for (int i = 0; i < listLayer.Count; ++i)
                    listLayer[i].CenterPos();
            }
        }
    }

    private void CheckSameLayer(List<CircleCollider> _listCollider)
    {
        using (s_tMarkerSameLayer.Auto())
        {
            for (int a = 0; a < _listCollider.Count; ++a)
            {
                CircleCollider refI = _listCollider[a];

                for (int b = a + 1; b < _listCollider.Count; ++b)
                    CheckPair(refI, _listCollider[b]);
            }
        }
    }

    private void CheckCrossLayer(List<CircleCollider> _listA, List<CircleCollider> _listB)
    {
        using (s_tMarkerCrossLayer.Auto())
        {
            for (int a = 0; a < _listA.Count; ++a)
            {
                CircleCollider refA = _listA[a];

                for (int b = 0; b < _listB.Count; ++b)
                    CheckPair(refA, _listB[b]);
            }
        }
    }

    private void CheckPair(CircleCollider _refA, CircleCollider _refB)
    {
        // 쿼터뷰: Y는 무시하고 XZ 평면 거리만으로 판정 (Bob 이펙트, 유도탄 넉백 등으로
        // Y가 살짝 어긋나 있어도 판정이 새지 않게 함). sqrMagnitude 대신 x,z만 직접 계산
        Vector3 vDelta = _refB.CachedCenter - _refA.CachedCenter;
        float fDistSq = vDelta.sqrMagnitude;
        float fRadiusSum = _refA.Radius + _refB.Radius;
        float fRadiusSumSq = fRadiusSum * fRadiusSum;

        long lKey = MakePairKey(_refA.ID, _refB.ID);
        bool bOverlapping = fDistSq <= fRadiusSumSq;
        //이번 프레임에 맞았다면
        if (bOverlapping)
        {
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
            //저번 프레임에서도 맞았다면
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

    // Physics.Raycast 대체용 - CircleCollider(PhysX 없음) 대상으로 레이 판정. Aim 등 화면 좌표
    // 기반 조준/커서 판정에서 사용. LayerMask는 값 타입이라 할당 없이 그대로 넘길 수 있음
    // (호출부에서 원하는 레이어를 미리 비트마스크로 조합해서 넘겨줌)
    public bool RaycastMask(Vector3 _vOrigin, Vector3 _vDir, float _fMaxLength, LayerMask _tMask, out CircleCollider _refHit)
    {
        _vOrigin.y = 0.0f;
        _vDir.y = 0.0f;
        _vDir.Normalize();

        CircleCollider refClosest = null;
        float fClosestT = float.MaxValue;

        for (int iLayer = 0; iLayer < 32; ++iLayer)
        {
            if ((_tMask.value & (1 << iLayer)) == 0)
                continue;

            List<CircleCollider> listLayer = m_arrCollider[iLayer];
            for (int i = 0; i < listLayer.Count; ++i)
            {
                CircleCollider refCollider = listLayer[i];
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

}
