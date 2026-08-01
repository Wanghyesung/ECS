using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
              ColliderManager
목적 : CircleCollider들을 XYZ 균일 그리드로 관리하며 PhysX 없이 원(구) 충돌을 직접 판정한다.

       - 특정 방향(공격자→타겟)을 가정하지 않는 대칭 구조: 등록된 CircleCollider를 전부
         하나의 List<CircleCollider>(ID로 인덱싱)로 관리하고, 인접 셀끼리 서로 비교한다.
       - 매 쌍(A,B)은 두 ID를 하나의 long 키로 합쳐(MakePairKey) Dictionary<long,bool>에
         "지금 겹쳐있는가"를 기록한다. 이번 프레임에 처음 겹치면 Enter, 지난 프레임에도
         true였으면 Stay, 지난 프레임엔 true였는데 이번엔 확인 안 되면 Exit.
       - 콜라이더별로 "현재 관여 중인 쌍" ID 목록(m_listPartners)을 별도로 들고 있다가,
         Deactivate 시 그 쌍 기록들을 즉시 정리한다 (풀 재사용으로 ID가 재활용될 때
         지난 생애의 낡은 쌍 정보가 남아있지 않도록).
       - 그리드 셀의 List<int>와 쌍 정리용 버퍼는 프레임마다 새로 할당하지 않고 재사용한다
         (GC Alloc 방지).
       - Bullet 등은 Rigidbody 기반 FixedUpdate로 이동하므로, 그 물리 스텝이 전부 끝나
         위치가 확정된 뒤인 LateUpdate에서 판정한다.
 *///////////////////////////////////////////

public class ColliderManager : MonoBehaviour
{
    public static ColliderManager m_Instance = null;

    [SerializeField] private float m_fCellSize = 2.0f;

    private List<CircleCollider> m_listCollider;
    private List<bool> m_listActive;

    // ID -> 지금 이 ID와 겹쳐있다고(pairState==true) 기록된 상대 ID들. Deactivate 정리용
    private List<HashSet<int>> m_listOther;

    // 셀 좌표 -> 그 셀에 있는 콜라이더 ID 목록. 셀의 List<int>는 한 번 생기면 계속 재사용됨
    private Dictionary<Vector3Int, List<int>> m_hashGrid;
    // 이번 프레임에 실제로 값이 채워진 셀만 추적해서, 다음 프레임 시작 시 그 셀들만 Clear
    private List<Vector3Int> m_listUsedCells;

    // 쌍(A,B) A<B -> 겹쳐있는지 여부. true인 채로 남아있다가 이번 프레임에 재확인 안 되면 Exit 처리 후 제거
    private Dictionary<long, bool> m_hashPairState;
    private HashSet<long> m_hashEnterCollider;
    private List<long> m_listExitBuffer; // 정리 단계에서 재사용하는 버퍼 (매 프레임 new 방지)

    private enum eHitEvent { Enter, Stay, Exit }

    private void Awake()
    {
        if (m_Instance != null)
        {
            Destroy(this);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(this);

        m_listCollider = new List<CircleCollider>();
        m_listActive = new List<bool>();
        m_listOther = new List<HashSet<int>>();

        m_hashGrid = new Dictionary<Vector3Int, List<int>>();
        m_listUsedCells = new List<Vector3Int>();

        m_hashPairState = new Dictionary<long, bool>();
        m_hashEnterCollider = new HashSet<long>();
        m_listExitBuffer = new List<long>();
    }

    private static long MakePairKey(int _iA, int _iB)
    {
        int iLow = _iA < _iB ? _iA : _iB;
        int iHigh = _iA < _iB ? _iB : _iA;
        return ((long)iLow << 32) | (uint)iHigh;
    }

    private static void DecodePairKey(long _lKey, out int _iA, out int _iB)
    {
        _iA = (int)(_lKey >> 32);
        _iB = (int)(_lKey & 0xFFFFFFFF);
    }

    // CircleCollider.Awake()에서 생애주기 중 딱 한 번만 호출. _iID는 CircleCollider의 static 카운터 값
    public void RegisterPermanent(CircleCollider _refOwner, int _iID)
    {
        // 리스트 크기를 ID에 맞춰 채워둠 (등록 순서 = ID 순서라 사실상 Add와 동일하게 채워짐)
        while (m_listCollider.Count <= _iID)
        {
            m_listCollider.Add(null);
            m_listActive.Add(false);
            m_listOther.Add(new HashSet<int>());
        }

        m_listCollider[_iID] = _refOwner;
    }

    // CircleCollider.OnEnable()에서 호출
    public void Activate(int _iID)
    {
        m_listActive[_iID] = true;
    }

    // CircleCollider.OnDisable()에서 호출
    public void UnActivate(int _iID)
    {
        m_listActive[_iID] = false;

        // 이 ID가 관여하던 쌍 기록을 전부 정리 (ID 재사용 시 낡은 쌍 정보가 남지 않도록)
        HashSet<int> hashOther = m_listOther[_iID];
        foreach (int iOther in hashOther)
        {
            m_hashPairState.Remove(MakePairKey(_iID, iOther));
            m_listOther[iOther].Remove(_iID);
        }
        hashOther.Clear();
    }

    private Vector3Int GetCell(Vector3 _vPos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(_vPos.x / m_fCellSize),
            Mathf.FloorToInt(_vPos.y / m_fCellSize),
            Mathf.FloorToInt(_vPos.z / m_fCellSize));
    }

    private void LateUpdate()
    {
        int iCount = m_listCollider.Count;
        if (iCount == 0)
            return;

        RebuildGrid(iCount);
        CheckOverlaps(iCount);
        ExitPair();
    }

    private void RebuildGrid(int _iCount)
    {
        // 지난 프레임에 쓰인 셀들의 List만 비움 (Dictionary 자체나 List 인스턴스는 재사용)
        for (int i = 0; i < m_listUsedCells.Count; ++i)
            m_hashGrid[m_listUsedCells[i]].Clear();
        m_listUsedCells.Clear();

        for (int i = 0; i < _iCount; ++i)
        {
            if (m_listActive[i] == false)
                continue;

            Vector3Int Vcell = GetCell(m_listCollider[i].Center);

            if (m_hashGrid.TryGetValue(Vcell, out var listIdx) == false)
            {
                listIdx = new List<int>(); // 이 셀 좌표가 생애 처음 쓰일 때만 할당, 이후 계속 재사용
                m_hashGrid[Vcell] = listIdx;
            }

            if (listIdx.Count == 0)
                m_listUsedCells.Add(Vcell);

            listIdx.Add(i);
        }
    }

    private void CheckOverlaps(int _iCount)
    {
        m_hashEnterCollider.Clear();

        for (int i = 0; i < _iCount; ++i)
        {
            if (!m_listActive[i])
                continue;

            CircleCollider refI = m_listCollider[i];
            Vector3 vPosI = refI.Center;
            Vector3Int cellI = GetCell(vPosI);

            //인접한 셀 게산
            for (int dx = -1; dx <= 1; ++dx)
            {
                for (int dy = -1; dy <= 1; ++dy)
                {
                    for (int dz = -1; dz <= 1; ++dz)
                    {
                        Vector3Int vCellNeighbor = new Vector3Int(cellI.x + dx, cellI.y + dy, cellI.z + dz);
                        if (!m_hashGrid.TryGetValue(vCellNeighbor, out var listIdx))
                            continue;

                        for (int k = 0; k < listIdx.Count; ++k)
                        {
                            int j = listIdx[k];
                            if (j <= i) // i<j인 쌍만 비교해서 중복 검사 방지 (자기 자신도 걸러짐)
                                continue;

                            CheckPair(i, refI, vPosI, j);
                        }
                    }
                }
            }
        }
    }

    private void CheckPair(int _iIndexA, CircleCollider _refA, Vector3 _vPosA, int _iIndexB)
    {
        CircleCollider refB = m_listCollider[_iIndexB];

        // 서로 관심 없는 레이어 조합(예: 총알끼리)이면 거리 계산도, 쌍 기록도 하지 않고 바로 스킵
        bool bACares = (_refA.LayerMask.value & (1 << refB.gameObject.layer)) != 0;
        bool bBCares = (refB.LayerMask.value & (1 << _refA.gameObject.layer)) != 0;
        if (!bACares && !bBCares)
            return;

        float fDistSq = (refB.Center - _vPosA).sqrMagnitude;
        float fRadiusSum = _refA.Radius + refB.Radius;
        if (fDistSq > fRadiusSum * fRadiusSum)
            return; // 안 겹치면 여기선 아무 것도 안 함 - Exit 판정은 정리 단계에서 일괄 처리

        long lKey = MakePairKey(_iIndexA, _iIndexB);
        m_hashEnterCollider.Add(lKey);

        bool bWasOverlapping = m_hashPairState.TryGetValue(lKey, out bool bPrev) && bPrev;

        if (!bWasOverlapping)
        {
            m_hashPairState[lKey] = true;
            m_listOther[_iIndexA].Add(_iIndexB);
            m_listOther[_iIndexB].Add(_iIndexA);

            OnTrriger(_refA,refB, eHitEvent.Enter);
        }
        else
            OnTrriger(_refA, refB, eHitEvent.Stay);
    }

    private void ExitPair()
    {
        m_listExitBuffer.Clear();

        foreach (var tValue in m_hashPairState)
        {
            if (tValue.Value && !m_hashEnterCollider.Contains(tValue.Key))
                m_listExitBuffer.Add(tValue.Key);
        }

        for (int n = 0; n < m_listExitBuffer.Count; ++n)
        {
            long lKey = m_listExitBuffer[n];
            DecodePairKey(lKey, out int iIndexA, out int iIndexB);

            if (m_listActive[iIndexA] && m_listActive[iIndexB])
                OnTrriger(m_listCollider[iIndexA],m_listCollider[iIndexB], eHitEvent.Exit);

            m_hashPairState.Remove(lKey);
            m_listOther[iIndexA].Remove(iIndexB);
            m_listOther[iIndexB].Remove(iIndexA);
        }
    }

    private void OnTrriger(CircleCollider _refA, CircleCollider _refB, eHitEvent _eEvent)
    {
        // 각자의 LayerMask로 독립적으로 판단 - 한쪽만 관심 있어도 그쪽만 이벤트를 받음
        bool bACares = (_refA.LayerMask.value & (1 << _refB.gameObject.layer)) != 0;
        bool bBCares = (_refB.LayerMask.value & (1 << _refA.gameObject.layer)) != 0;

        switch (_eEvent)
        {
            case eHitEvent.Enter:
                if (bACares) _refA.OnEnterCollider(_refB);
                if (bBCares) _refB.OnEnterCollider(_refA);
                break;
            case eHitEvent.Stay:
                if (bACares) _refA.OnStayCollider(_refB);
                if (bBCares) _refB.OnStayCollider(_refA);
                break;
            case eHitEvent.Exit:
                if (bACares) _refA.OnExitCollider(_refB);
                if (bBCares) _refB.OnExitCollider(_refA);
                break;
        }
    }
}
