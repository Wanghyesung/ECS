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
         CheckPair는 거리 비교로 안 겹치는 게 확인되면 Dictionary를 아예 안 건드리고 바로
         return한다 - 대부분의 검사(총알-몬스터 후보 쌍 대부분은 안 겹침)가 해싱/버킷 탐색
         없이 순수 산술 비교만으로 끝난다는 뜻. 실제로 겹친 쌍만 m_hashConfirmedPair에
         표시해두고, 매 프레임 끝에 m_hashPairInfo(항상 "지금 겹치는 쌍"만 들어있어 작음)를
         순회하며 이번 프레임에 재확인 안 된 것만 Exit 처리한다(ExitStalePairs) -
         "쌍 전체 후보"가 아니라 "실제로 겹치는 쌍"에서만 Dictionary 비용이 발생함.
       - 콜라이더별로 "현재 관여 중인 쌍" ID 목록(m_listOther)을 별도로 들고 있다가,
         UnActivate 시 그 쌍 기록들을 즉시 정리한다(겹친 채로 반납되면 Exit도 쏴줌).
       - CircleCollider.Center는 쿼터니언 곱셈이 들어있어 쌍마다 반복 조회하면 낭비가 크다
         (총알 하나가 몬스터 수만큼, 몬스터 하나가 총알 수만큼 매번 다시 계산됨). 판정 루프
         돌기 전에 RefreshAllCenters로 활성 콜라이더마다 프레임당 딱 한 번만 계산해서
         캐시해두고, CheckPair는 CachedCenter만 읽는다.
       - Bullet 등은 Update에서 Transform을 직접 이동하므로, 그게 전부 끝나 위치가 확정된
         뒤인 LateUpdate에서 판정한다.
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

    // ID -> 그 콜라이더가 자기 레이어 리스트에서 몇 번째 자리인지 (UnActivate 스왑백 O(1) 제거용)
    private List<int> m_listIndexInLayerList;

    // ID -> 지금 이 ID와 실제로 겹쳐있다고 기록된 상대 ID들. UnActivate 시 관련 쌍 정리용
    private List<HashSet<int>> m_listOther;

    // 쌍(A,B) -> 항목이 존재 = 지금 겹치고 있다는 뜻 (Enter 시 생성, Exit 시 제거)
    private Dictionary<long, tColliderPair> m_hashPairInfo;

    // 이번 프레임에 실제로 겹친 것으로 재확인된 쌍 키. ExitStalePairs에서 "재확인 안 된 것"
    // 판별용으로만 쓰고 매 프레임 끝에 비움 (실제로 겹치는 쌍 수만큼만 존재 - 작은 집합)
    private HashSet<long> m_hashConfirmedPair;

    // ExitStalePairs가 m_hashPairInfo를 순회하며 지울 키를 모아두는 재사용 버퍼
    // (Dictionary를 순회하며 바로 Remove할 수 없어서 필요. 매 프레임 새로 할당 안 함)
    private List<long> m_listExitBuffer;

    // 프로파일러 Hierarchy 뷰에서 Deep Profile 없이도 구간별 비용을 이름 붙여서 볼 수 있게
    // 하는 마커. CheckPair는 프레임당 수만 번 불리므로 마커 자체의 오버헤드가 측정을
    // 왜곡할 수 있어 넣지 않음(CheckSameLayer/CheckCrossLayer 상위 레벨에서만 구분)
    //private static readonly ProfilerMarker s_tMarkerSameLayer = new ProfilerMarker("ColliderManager.CheckSameLayer");
    //private static readonly ProfilerMarker s_tMarkerCrossLayer = new ProfilerMarker("ColliderManager.CheckCrossLayer");
    //private static readonly ProfilerMarker s_tMarkerExitStale = new ProfilerMarker("ColliderManager.ExitStalePairs");
    //private static readonly ProfilerMarker s_tMarkerRefreshCenter = new ProfilerMarker("ColliderManager.RefreshAllCenters");

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

        m_listIndexInLayerList = new List<int>();
        m_listOther = new List<HashSet<int>>();

        m_hashPairInfo = new Dictionary<long, tColliderPair>();
        m_hashConfirmedPair = new HashSet<long>();
        m_listExitBuffer = new List<long>();
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

    // CircleCollider.OnEnable()에서 호출 - 그 즉시 자기 레이어 리스트에 편입
    public void Activate(CircleCollider _refCollider)
    {
        ResizeCapacity(_refCollider.ID);

        List<CircleCollider> listLayer = m_arrCollider[_refCollider.Layer];
        m_listIndexInLayerList[_refCollider.ID] = listLayer.Count;
        listLayer.Add(_refCollider);
    }

    // CircleCollider.OnDisable()에서 호출 - 그 즉시 자기 레이어 리스트에서 스왑백 제거
    public void UnActivate(CircleCollider _refCollider)
    {
        int iID = _refCollider.ID;
        List<CircleCollider> listLayer = m_arrCollider[_refCollider.Layer];

        int iMyIndex = m_listIndexInLayerList[iID];
        int iLastIndex = listLayer.Count - 1;

        CircleCollider refMoved = listLayer[iLastIndex];
        listLayer[iMyIndex] = refMoved;
        listLayer.RemoveAt(iLastIndex);
        m_listIndexInLayerList[refMoved.ID] = iMyIndex;
        m_listIndexInLayerList[iID] = -1;

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
    }

    private void LateUpdate()
    {
        CheckOverlaps();
        ExitStalePairs();
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

    // Center(쿼터니언 곱셈 포함)를 쌍마다 다시 계산하지 않도록, 활성 콜라이더마다 프레임당
    // 딱 한 번만 미리 계산해서 캐시해둔다 (총알 하나가 몬스터 수만큼 반복 계산되던 것을 방지)
    private void PreLoadCenter()
    {
        //using (s_tMarkerRefreshCenter.Auto())
        //{
        for (int iLayer = 0; iLayer < 32; ++iLayer)
        {
            List<CircleCollider> listLayer = m_arrCollider[iLayer];
            for (int i = 0; i < listLayer.Count; ++i)
                listLayer[i].CenterPos();
        }
        //}
    }

    private void CheckSameLayer(List<CircleCollider> _listCollider)
    {
        //using (s_tMarkerSameLayer.Auto())
        //{
        for (int a = 0; a < _listCollider.Count; ++a)
        {
            CircleCollider refI = _listCollider[a];

            for (int b = a + 1; b < _listCollider.Count; ++b)
                CheckPair(refI, _listCollider[b]);
        }
        //}
    }

    private void CheckCrossLayer(List<CircleCollider> _listA, List<CircleCollider> _listB)
    {
        //using (s_tMarkerCrossLayer.Auto())
        //{
        for (int a = 0; a < _listA.Count; ++a)
        {
            CircleCollider refA = _listA[a];

            for (int b = 0; b < _listB.Count; ++b)
                CheckPair(refA, _listB[b]);
        }
        //}
    }

    private void CheckPair(CircleCollider _refA, CircleCollider _refB)
    {
        float fDistSq = (_refB.CachedCenter - _refA.CachedCenter).sqrMagnitude;
        float fRadiusSum = _refA.Radius + _refB.Radius;

        // 안 겹치면 Dictionary는 아예 안 건드리고 바로 끝 - 후보 쌍 대부분이 여기서 끝남
        if (fDistSq > fRadiusSum * fRadiusSum)
            return;

        long lKey = MakePairKey(_refA.ID, _refB.ID);
        m_hashConfirmedPair.Add(lKey);

        if (m_hashPairInfo.TryGetValue(lKey, out tColliderPair tPair))
        {
            tPair.ColliderA.OnStayCollider(tPair.ColliderB);
            tPair.ColliderB.OnStayCollider(tPair.ColliderA);
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

    // m_hashPairInfo(=지금 겹치는 쌍만 들어있는 작은 집합)를 순회하며, 이번 프레임에
    // CheckPair로 재확인 안 된 것만 Exit 처리. "안 겹치는 후보 쌍 전체"가 아니라
    // "실제로 겹치던 쌍"만 순회하니 총알-몬스터 후보 수와 무관하게 항상 저렴함
    private void ExitStalePairs()
    {
        //using (s_tMarkerExitStale.Auto())
        //{
        m_listExitBuffer.Clear();

        foreach (var tKv in m_hashPairInfo)
        {
            if (!m_hashConfirmedPair.Contains(tKv.Key))
                m_listExitBuffer.Add(tKv.Key);
        }

        for (int i = 0; i < m_listExitBuffer.Count; ++i)
        {
            long lKey = m_listExitBuffer[i];
            tColliderPair tPair = m_hashPairInfo[lKey];

            tPair.ColliderA.OnExitCollider(tPair.ColliderB);
            tPair.ColliderB.OnExitCollider(tPair.ColliderA);

            m_hashPairInfo.Remove(lKey);
            m_listOther[tPair.ColliderA.ID].Remove(tPair.ColliderB.ID);
            m_listOther[tPair.ColliderB.ID].Remove(tPair.ColliderA.ID);
        }
    }

}
