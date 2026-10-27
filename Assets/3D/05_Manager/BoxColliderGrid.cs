using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

// 최초 범위로 고정된 공간 그리드. 매 프레임 셀별 SoA 인덱스를 카운팅 소트로 재구성한다.
public sealed class BoxColliderGrid
{
    private const float MIN_CELL_SIZE = 1f;
    private const int MIN_ITEM_CAPACITY = 64;

    // 셀 인덱스 -> CellItems에서 그 셀 구간이 시작하는 위치 / 그 셀의 아이템 개수
    private NativeArray<int> m_arrCellStart;
    private NativeArray<int> m_arrCellCount;

    // 셀 순으로 정렬된 아이템(= Box SoA dense 인덱스) 목록
    private NativeArray<int> m_arrCellItems;

    // 재구성용 스크래치 - 아이템별 소속 셀 인덱스 / 셀별 다음 삽입 위치 커서
    private NativeArray<int> m_arrItemToCell;
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

    // Job에 전달하는 그리드 파라미터.
    public Vector3 Origin => m_vOrigin;
    public float CellSize => m_fCellSize;
    public int CountX => m_iCountX;
    public int CountY => m_iCountY;
    public int CountZ => m_iCountZ;
    public int ItemCount => m_iColliderCount;

    public NativeArray<int> CellStart => m_arrCellStart;
    public NativeArray<int> CellCount => m_arrCellCount;
    public NativeArray<int> CellItems => m_arrCellItems;

    // 최초 분포와 최대 반지름으로 범위와 셀 크기를 정한다.
    public void Build(List<BaseCollider> _listCollider)
    {
        m_bBuilt = false;
        if (_listCollider == null || _listCollider.Count == 0)
            return;

        //가장 큰 바운더리 값을 가진 Collider를 기준으로 셀 나누기
        Vector3 vMin = _listCollider[0].CachedCenter;
        Vector3 vMax = vMin;
        float fMaxRadius = 0f;

        for (int i = 0; i < _listCollider.Count; ++i)
        {
            BaseCollider refCollider = _listCollider[i];
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

        // 빈 그리드도 Job에 전달할 수 있도록 최소 용량을 할당한다.
        ResizeColliderCapacity(MIN_ITEM_CAPACITY);

        m_iColliderCount = 0;
        m_bBuilt = true;
    }

    // Build 완료 후 BeginRebuild → AddCollider → EndRebuild 순서로 재구성한다.

    // _iCount는 이번 프레임에 들어올 수 있는 아이템 수의 상한(컬링 전 개수)이면 충분
    public void BeginRebuild(int _iCount)
    {
        ResizeColliderCapacity(_iCount);

        for (int i = 0; i < m_iTotalCell; ++i)
            m_arrCellCount[i] = 0;

        m_iColliderCount = 0;
    }

    // _iIdx는 SoA와 같은 인덱스를 0부터 연속으로 전달한다.
    public void AddCollider(int _iIdx, Vector3 _vWorldPos)
    {
        ComputeCellCoord(_vWorldPos, m_vOrigin, m_fCellSize, m_iCountX, m_iCountY, m_iCountZ,
            out int iX, out int iY, out int iZ);

        int iCell = FlattenIndex(iX, iY, iZ, m_iCountX, m_iCountY);

        m_arrItemToCell[_iIdx] = iCell;     //내 콜라이더가 어느 셀에 있는지
        m_arrCellCount[iCell] += 1;         //해당 셀에 속한 콜라이더 수

        ++m_iColliderCount;
    }

    // 프리픽스 합으로 셀별 시작 위치를 잡고, 아이템을 자기 셀 구간으로 흩뿌린다
    public void EndRebuild()
    {
        int iRunning = 0;
        for (int i = 0; i < m_iTotalCell; ++i)
        {
            m_arrCellStart[i] = iRunning;       //해당 셀에 존재하는 콜라이더 수 (오프셋)
            m_arrScratchCursor[i] = iRunning;   //작업용 커서 - 시작 위치로 초기화
            iRunning += m_arrCellCount[i];      // 다음 셀의 콜라이더 시작 위치
        }

        for (int i = 0; i < m_iColliderCount; ++i)
        {
            int iCell = m_arrItemToCell[i];              //ColliderID에 맞는 Cell위치
            int iSlot = m_arrScratchCursor[iCell];       //셀의 위치의 작업 시작 위치
            m_arrCellItems[iSlot] = i;
            m_arrScratchCursor[iCell] = iSlot + 1;       //셀의 다음 작업 시작 위치
        }
    }

    public void Dispose()
    {
        DisposeCellArrays();

        if (m_arrCellItems.IsCreated)
            m_arrCellItems.Dispose();
        if (m_arrItemToCell.IsCreated)
            m_arrItemToCell.Dispose();

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

    // 매 프레임 덮어쓰는 버퍼이므로 확장 시 기존 데이터는 복사하지 않는다.
    private void ResizeColliderCapacity(int _iCount)
    {
        if (m_arrCellItems.IsCreated && m_arrCellItems.Length >= _iCount)
            return;

        int iNewCapacity = m_arrCellItems.IsCreated ? m_arrCellItems.Length : MIN_ITEM_CAPACITY;
        while (iNewCapacity < _iCount)
            iNewCapacity <<= 1;

        if (m_arrCellItems.IsCreated)
            m_arrCellItems.Dispose();
        if (m_arrItemToCell.IsCreated)
            m_arrItemToCell.Dispose();

        m_arrCellItems = new NativeArray<int>(iNewCapacity, Allocator.Persistent);
        m_arrItemToCell = new NativeArray<int>(iNewCapacity, Allocator.Persistent);
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
