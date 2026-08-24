using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

/*///////////////////////////////////////////
              BoxColliderGrid
목적 : 씬에 활성화된 콜라이더 전부(Shape/레이어 무관)를 한 번에 담는 단일 정적 공간분할
       그리드 - ColliderManager가 이 그리드 하나만 인스턴스로 들고 매 프레임 재사용한다.
       클래스 이름은 Box 전용이던 시절의 흔적이지만, Build()는 BaseCollider.BoundingRadius
       (다형)만 사용해 도형 무관. 브로드페이즈 조회를 Burst Job(ColliderManager.
       GridOverlapJob)이 담당하게 되면서, 셀 내용을 관리형 List<BaseCollider>가 아니라
       NativeArray<int> flat 구조(카운팅 소트)로 들고 있는다 - Job은 관리형 타입을
       전혀 만질 수 없기 때문.

       구조: CellStart/CellCount(셀 개수만큼) + CellItems(콜라이더 개수만큼)의 고전적인
       카운팅 소트 결과물. CellItems에는 BaseCollider 참조도 ID도 아닌 "이번 프레임
       ColliderManager SoA 배열에서의 dense 인덱스"가 들어간다 - 조회 측(Job)이 곧바로
       Center[i]/AxisX[i] 같은 연속 배열을 인덱싱할 수 있게 하기 위함.

       [,,] 다차원 배열이 아니라 1차원 + 수동 플래튼(x + y*W + z*W*H)을 쓰는 이유는
       기존과 동일(다차원 인덱싱이 더 느림).

       그리드 범위(셀 개수/원점/셀 크기)는 Build 시점에 넘겨받은 콜라이더들의 실제
       분포(CachedCenter ± BoundingRadius의 합집합)로 딱 한 번 정해진다("정적 분할").
       매 프레임 바뀌는 건 셀 소속(멤버십)뿐이고, 그건 BeginRebuild→AddCollider→EndRebuild로
       통째로 다시 계산한다. 그리드 범위 밖 위치는 가장 가까운 가장자리 셀로 클램프된다.

       "매 프레임 전체 재구성"이라 RemoveCollider 같은 개별 제거 API가 없다 - 사거리
       컬링으로 빠지는 Box(Obstacle)는 그냥 이번 프레임 AddCollider 대상에서 제외하면 그만이고,
       파괴/풀 반납된 콜라이더도 다음 재구성에서 자연히 사라진다.

       콜라이더 하나는 정확히 셀 하나에만 속해서(자기 중심이 속한 셀) 이웃 27칸 조회 시
       중복 제거가 필요 없다. 이웃 1겹만으로 실제 겹침을 놓치지 않으려면 셀 크기가
       "겹칠 수 있는 두 콜라이더 반지름의 합"보다 커야 한다는 전제가 있고, Build가 그
       시점 최대 BoundingRadius*2로 셀 크기를 잡아 그걸 책임진다.
 *///////////////////////////////////////////
public sealed class BoxColliderGrid
{
    private const float MIN_CELL_SIZE = 1f;
    private const int MIN_ITEM_CAPACITY = 64;

    // 셀 인덱스 -> CellItems에서 그 셀 구간이 시작하는 위치 / 그 셀의 아이템 개수
    private NativeArray<int> m_arrCellStart;
    private NativeArray<int> m_arrCellCount;

    // 셀 순으로 정렬된 아이템(= Box SoA dense 인덱스) 목록
    private NativeArray<int> m_arrCellCollider;

    // 재구성용 스크래치 - 아이템별 소속 셀 인덱스 / 셀별 다음 삽입 위치 커서
    private NativeArray<int> m_arrScratchCellIndexPerItem;
    private NativeArray<int> m_arrScratchCursor;

    private Vector3 m_vOrigin;
    private float m_fCellSize = MIN_CELL_SIZE;
    private int m_iCountX = 1;
    private int m_iCountY = 1;
    private int m_iCountZ = 1;
    private int m_iTotalCell;
    private int m_iColliderCount;
    private bool m_bBuilt;

    public bool IsBuilt => m_bBuilt;

    // Job이 자기 필드로 복사해가는 그리드 파라미터 - Job은 BoxColliderGrid 인스턴스
    // 자체(관리형 클래스)를 들고 갈 수 없으므로 값만 넘겨받는다
    public Vector3 Origin => m_vOrigin;
    public float CellSize => m_fCellSize;
    public int CountX => m_iCountX;
    public int CountY => m_iCountY;
    public int CountZ => m_iCountZ;
    public int ItemCount => m_iColliderCount;

    public NativeArray<int> CellStart => m_arrCellStart;
    public NativeArray<int> CellCount => m_arrCellCount;
    public NativeArray<int> CellItems => m_arrCellCollider;

    // 이 그리드를 소유한 레이어(콜라이더 목록)의 실제 분포로 그리드 범위를 딱 한 번 정한다("정적 분할").
    // 셀 크기는 그 시점 최대 BoundingRadius*2 - 이웃 1겹 검사만으로 놓치지 않기 위한 하한
    public void Build(List<BaseCollider> _listOwnerCollider)
    {
        m_bBuilt = false;
        if (_listOwnerCollider == null || _listOwnerCollider.Count == 0)
            return;

        //가장 큰 바운더리 값을 가진 Collider를 기준으로 셀 나누기
        Vector3 vMin = _listOwnerCollider[0].CachedCenter;
        Vector3 vMax = vMin;
        float fMaxRadius = 0f;

        for (int i = 0; i < _listOwnerCollider.Count; ++i)
        {
            BaseCollider refCollider = _listOwnerCollider[i];
            float fRadius = refCollider.BoundingRadius;
            Vector3 vCenter = refCollider.CachedCenter;
            Vector3 vRadiusVec = Vector3.one * fRadius;

            vMin = Vector3.Min(vMin, vCenter - vRadiusVec);
            vMax = Vector3.Max(vMax, vCenter + vRadiusVec);

            if (fRadius > fMaxRadius)
                fMaxRadius = fRadius;
        }

        m_fCellSize = Mathf.Max(MIN_CELL_SIZE, fMaxRadius * 2f);
        m_vOrigin = vMin;

        Vector3 vSize = vMax - vMin;
        m_iCountX = Mathf.Max(1, Mathf.CeilToInt(vSize.x / m_fCellSize));
        m_iCountY = Mathf.Max(1, Mathf.CeilToInt(vSize.y / m_fCellSize));
        m_iCountZ = Mathf.Max(1, Mathf.CeilToInt(vSize.z / m_fCellSize));

        m_iTotalCell = m_iCountX * m_iCountY * m_iCountZ;

        DisposeCellArrays();
        m_arrCellStart = new NativeArray<int>(m_iTotalCell, Allocator.Persistent);
        m_arrCellCount = new NativeArray<int>(m_iTotalCell, Allocator.Persistent);
        m_arrScratchCursor = new NativeArray<int>(m_iTotalCell, Allocator.Persistent);

        // 아직 아이템이 없어도 Job에 넘길 수 있게(할당 안 된 NativeArray를 Job 필드로
        // 넘기면 안전 시스템이 예외를 던짐) 최소 용량으로 미리 잡아둔다
        ResizeColliderCapacity(MIN_ITEM_CAPACITY);

        m_iColliderCount = 0;
        m_bBuilt = true;
    }

    // --- 매 프레임 재구성(카운팅 소트) : BeginRebuild -> AddCollider * N -> EndRebuild ---

    // _iCount는 이번 프레임에 들어올 수 있는 아이템 수의 상한(컬링 전 개수)이면 충분
    public void BeginRebuild(int _iCount)
    {
        if (m_bBuilt == false)
            return;

        ResizeColliderCapacity(_iCount);

        for (int i = 0; i < m_iTotalCell; ++i)
            m_arrCellCount[i] = 0;

        m_iColliderCount = 0;
    }

    // _iIdx는 호출부(ColliderManager)가 Box SoA에 쓰는 것과 동일한 인덱스여야 한다
    public void AddCollider(int _iIdx, Vector3 _vWorldPos)
    {
        if (m_bBuilt == false)
            return;

        ComputeCellCoord(_vWorldPos, m_vOrigin, m_fCellSize, m_iCountX, m_iCountY, m_iCountZ,
            out int iX, out int iY, out int iZ);

        int iCell = FlattenIndex(iX, iY, iZ, m_iCountX, m_iCountY);

        m_arrScratchCellIndexPerItem[_iIdx] = iCell;
        m_arrCellCount[iCell] = m_arrCellCount[iCell] + 1;

        // 호출부는 0부터 순서대로 넘기지만, 순서가 어긋나도 EndRebuild가 전 구간을 훑도록 상한을 잡는다
        if (_iIdx + 1 > m_iColliderCount)
            m_iColliderCount = _iIdx + 1;
    }

    // 프리픽스 합으로 셀별 시작 위치를 잡고, 아이템을 자기 셀 구간으로 흩뿌린다
    public void EndRebuild()
    {
        if (m_bBuilt == false)
            return;

        int iRunning = 0;
        for (int i = 0; i < m_iTotalCell; ++i)
        {
            m_arrCellStart[i] = iRunning;   //해당 셀에 존재하는 콜라이더 수 (오프셋)
            m_arrScratchCursor[i] = iRunning;//작업용 커서 - 시작 위치로 초기화
            iRunning += m_arrCellCount[i]; // 다음 셀의 콜라이더 시작 위치
        }

        for (int i = 0; i < m_iColliderCount; ++i)
        {
            int iCell = m_arrScratchCellIndexPerItem[i]; //ColliderID에 맞는 Cell위치
            int iSlot = m_arrScratchCursor[iCell];       //셀의 위치의 작업 시작 위치
            m_arrCellCollider[iSlot] = i;
            m_arrScratchCursor[iCell] = iSlot + 1;       //셀의 다음 작업 시작 위치
        }
    }

    public void Dispose()
    {
        DisposeCellArrays();

        if (m_arrCellCollider.IsCreated)
            m_arrCellCollider.Dispose();
        if (m_arrScratchCellIndexPerItem.IsCreated)
            m_arrScratchCellIndexPerItem.Dispose();

        m_bBuilt = false;
        m_iColliderCount = 0;
    }

    // --- 좌표 계산(메인스레드 재구성과 Job이 반드시 같은 함수를 쓰도록 public static) ---

    // 그리드 범위 밖 위치는 가장 가까운 가장자리 셀로 클램프한다
    public static void ComputeCellCoord(
        Vector3 _vWorldPos, Vector3 _vOrigin, float _fCellSize,
        int _iCountX, int _iCountY, int _iCountZ,
        out int _iX, out int _iY, out int _iZ)
    {
        Vector3 vLocal = _vWorldPos - _vOrigin;
        _iX = Mathf.Clamp(Mathf.FloorToInt(vLocal.x / _fCellSize), 0, _iCountX - 1);
        _iY = Mathf.Clamp(Mathf.FloorToInt(vLocal.y / _fCellSize), 0, _iCountY - 1);
        _iZ = Mathf.Clamp(Mathf.FloorToInt(vLocal.z / _fCellSize), 0, _iCountZ - 1);
    }

    public static int FlattenIndex(int _iX, int _iY, int _iZ, int _iCountX, int _iCountY)
    {
        return (_iZ * _iCountY + _iY) * _iCountX + _iX;
    }

    // --- 내부 ---

    // 프레임 스크래치 버퍼라 이전 내용을 보존할 필요가 없다(매 프레임 통째로 덮어씀) -
    // 모자라면 Dispose 후 더블링 크기로 새로 잡는다(복사 불필요)
    private void ResizeColliderCapacity(int _iCount)
    {
        if (m_arrCellCollider.IsCreated && m_arrCellCollider.Length >= _iCount)
            return;

        int iNewCapacity = m_arrCellCollider.IsCreated ? m_arrCellCollider.Length : MIN_ITEM_CAPACITY;
        while (iNewCapacity < _iCount)
            iNewCapacity <<= 1;

        if (m_arrCellCollider.IsCreated)
            m_arrCellCollider.Dispose();
        if (m_arrScratchCellIndexPerItem.IsCreated)
            m_arrScratchCellIndexPerItem.Dispose();

        m_arrCellCollider = new NativeArray<int>(iNewCapacity, Allocator.Persistent);
        m_arrScratchCellIndexPerItem = new NativeArray<int>(iNewCapacity, Allocator.Persistent);
    }

    private void DisposeCellArrays()
    {
        if (m_arrCellStart.IsCreated)
            m_arrCellStart.Dispose();
        if (m_arrCellCount.IsCreated)
            m_arrCellCount.Dispose();
        if (m_arrScratchCursor.IsCreated)
            m_arrScratchCursor.Dispose();
    }
}
