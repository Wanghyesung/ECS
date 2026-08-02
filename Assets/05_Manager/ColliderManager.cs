using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
              ColliderManager
목적 : CircleCollider들을 PhysX 없이 자체적으로 원(구) 충돌 판정한다.

       - 레이어 0~31 각각에 대응하는 List<CircleCollider>(그 레이어에 속한 활성 콜라이더)를
         Awake 시점에 전부 미리 만들어둔다. 레이어 종류는 32개로 고정돼 있으니 통째로 미리
         만들어도 비용이 전혀 없음.
       - 이 리스트들은 매 프레임 다시 채우지 않는다. CircleCollider가 Activate/UnActivate될
         때(=발사/반납 시점) 그 즉시 자기 레이어 리스트에 추가/제거된다. 제거는 "내가 그
         리스트에서 몇 번째 자리인지"를 ID 기준으로 따로 기록해뒀다가 스왑백으로 O(1)에 처리
         (선형 탐색/람다 캡처 없음 - GC Alloc 방지).
       - 어떤 레이어끼리 충돌할지는 개별 콜라이더가 아니라 이 매니저가 중앙에서 결정한다
         (Unity 프로젝트 세팅의 Physics 레이어 충돌 매트릭스와 동일한 개념).
       - 공간 그리드(셀 분할)는 쓰지 않는다. 총알처럼 개체 수가 많은 쪽과 몬스터처럼 개체 수가
         적은 쪽이 충돌하는 게임 특성상, 레이어 리스트끼리 전부 대조하는 게 그리드보다 더 빠름.
       - 쌍(A,B) 정보(m_hashPairInfo)는 "실제로 겹친 순간"에만 생성하고, 겹침이 끝나면
         (Exit) 바로 제거한다. 겹친 적 없는 쌍은 매 프레임 검사만 하고 아무 기록도 안 남긴다
         - 그렇지 않으면 검사한 모든 쌍(총알 하나가 스쳐 지나간 몬스터 전부 포함)이 영원히
         쌓이는 누수가 된다.
       - 콜라이더별로 "현재 관여 중인 쌍" ID 목록(m_listOther)을 별도로 들고 있다가,
         UnActivate 시 그 쌍 기록들을 즉시 정리한다(겹친 채로 반납되면 Exit도 쏴줌).
       - Bullet 등은 Update에서 Transform을 직접 이동하므로, 그게 전부 끝나 위치가 확정된
         뒤인 LateUpdate에서 판정한다.
 *///////////////////////////////////////////

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

    // 쌍(A,B) -> 실제로 겹친 순간에만 생성되는 정보. struct라 Dictionary에 다시 써줘야 갱신됨
    private Dictionary<long, tColliderPair> m_hashPairInfo;

    private struct tColliderPair
    {
        public CircleCollider ColliderA;
        public CircleCollider ColliderB;
        public bool OnCollision;
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
    private void EnsureCapacity(int _iID)
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
        EnsureCapacity(_refCollider.ID);
    }

    // CircleCollider.OnEnable()에서 호출 - 그 즉시 자기 레이어 리스트에 편입
    public void Activate(CircleCollider _refCollider)
    {
        EnsureCapacity(_refCollider.ID);

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

        // 이 콜라이더가 관여하던 쌍 기록을 전부 정리 (겹친 채로 반납되면 Exit도 쏴줌)
        HashSet<int> hashOther = m_listOther[iID];
        foreach (int iOtherID in hashOther)
        {
            long lKey = MakePairKey(iID, iOtherID);
            if (m_hashPairInfo.TryGetValue(lKey, out tColliderPair tPair))
            {
                if (tPair.OnCollision)
                {
                    tPair.ColliderA.OnExitCollider(tPair.ColliderB);
                    tPair.ColliderB.OnExitCollider(tPair.ColliderA);
                }
                m_hashPairInfo.Remove(lKey);
            }
            m_listOther[iOtherID].Remove(iID);
        }
        hashOther.Clear();
    }

    private void LateUpdate()
    {
        CheckOverlaps();
    }

    private void CheckOverlaps()
    {
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

    private void CheckSameLayer(List<CircleCollider> _listCollider)
    {
        for (int a = 0; a < _listCollider.Count; ++a)
        {
            CircleCollider refI = _listCollider[a];

            for (int b = a + 1; b < _listCollider.Count; ++b)
                CheckPair(refI, _listCollider[b]);
        }
    }

    private void CheckCrossLayer(List<CircleCollider> _listA, List<CircleCollider> _listB)
    {
        for (int a = 0; a < _listA.Count; ++a)
        {
            CircleCollider refA = _listA[a];

            for (int b = 0; b < _listB.Count; ++b)
                CheckPair(refA, _listB[b]);
        }
    }

    private void CheckPair(CircleCollider _refA, CircleCollider _refB)
    {
        float fDistSq = (_refB.Center - _refA.Center).sqrMagnitude;
        float fRadiusSum = _refA.Radius + _refB.Radius;
        bool bCollision = fDistSq <= fRadiusSum * fRadiusSum; // 거리가 반지름 합 이하일 때 겹침

        long lKey = MakePairKey(_refA.ID, _refB.ID);
        bool bFound = m_hashPairInfo.TryGetValue(lKey, out tColliderPair tPair);

        if (!bFound)
        {
            // 겹친 적도 없는데 기록만 만들면 검사한 모든 쌍이 영원히 쌓이는 누수가 되므로,
            // 실제로 겹칠 때만 새로 만든다
            if (!bCollision)
                return;

            tPair = new tColliderPair { ColliderA = _refA, ColliderB = _refB, OnCollision = false };
            m_listOther[_refA.ID].Add(_refB.ID);
            m_listOther[_refB.ID].Add(_refA.ID);
        }

        if (bCollision)
        {
            if (tPair.OnCollision)
            {
                tPair.ColliderA.OnStayCollider(tPair.ColliderB);
                tPair.ColliderB.OnStayCollider(tPair.ColliderA);
            }
            else
            {
                tPair.OnCollision = true;
                tPair.ColliderA.OnEnterCollider(tPair.ColliderB);
                tPair.ColliderB.OnEnterCollider(tPair.ColliderA);
            }

            m_hashPairInfo[lKey] = tPair; // struct라 값이 바뀌면 다시 써줘야 Dictionary에 반영됨
        }
        else
        {
            // bFound==false && bCollision==false는 위에서 이미 return 했으므로 여기 온 건 항상 bFound==true
            tPair.ColliderA.OnExitCollider(tPair.ColliderB);
            tPair.ColliderB.OnExitCollider(tPair.ColliderA);

            m_hashPairInfo.Remove(lKey);
            m_listOther[_refA.ID].Remove(_refB.ID);
            m_listOther[_refB.ID].Remove(_refA.ID);
        }
    }
}
