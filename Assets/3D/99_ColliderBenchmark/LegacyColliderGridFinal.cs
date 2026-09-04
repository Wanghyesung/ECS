using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

/*///////////////////////////////////////////
                LegacyColliderGridFinal
목적 : 챕터 4 전용 그리드 - 알고리즘은 LegacyColliderGridUnified(챕터 3)와 완전히
       동일하고, 타입만 LegacyBruteForceColliderFinal로 바뀐다(이 챕터의 콜라이더
       베이스가 별도 타입이라 그리드도 같이 갈라짐).
 *///////////////////////////////////////////
public sealed class LegacyColliderGridFinal
{
    private const float MIN_CELL_SIZE = 1f;
    private const int MIN_ITEM_CAPACITY = 64;

    private NativeArray<int> m_arrCellStart;
    private NativeArray<int> m_arrCellCount;
    private NativeArray<int> m_arrCellItems;

    private NativeArray<int> m_arrScratchCellIndexPerItem;
    private NativeArray<int> m_arrScratchCursor;

    private Vector3 m_vOrigin;
    private float m_fCellSize = MIN_CELL_SIZE;
    private int m_iCountX = 1;
    private int m_iCountY = 1;
    private int m_iCountZ = 1;
    private int m_iTotalCell;
    private int m_iItemCount;
    private bool m_bBuilt;

    public bool IsBuilt => m_bBuilt;

    public Vector3 Origin => m_vOrigin;
    public float CellSize => m_fCellSize;
    public int CountX => m_iCountX;
    public int CountY => m_iCountY;
    public int CountZ => m_iCountZ;

    public NativeArray<int> CellStart => m_arrCellStart;
    public NativeArray<int> CellCount => m_arrCellCount;
    public NativeArray<int> CellItems => m_arrCellItems;

    public void Build(List<LegacyBruteForceColliderFinal> _listAllActive)
    {
        m_bBuilt = false;
        if (_listAllActive == null || _listAllActive.Count == 0)
            return;

        Vector3 vMin = _listAllActive[0].CachedCenter;
        Vector3 vMax = vMin;
        float fMaxRadius = 0f;

        for (int i = 0; i < _listAllActive.Count; ++i)
        {
            LegacyBruteForceColliderFinal refCollider = _listAllActive[i];
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

        EnsureItemCapacity(MIN_ITEM_CAPACITY);

        m_iItemCount = 0;
        m_bBuilt = true;
    }

    public void BeginRebuild(int _iCount)
    {
        if (m_bBuilt == false)
            return;

        EnsureItemCapacity(_iCount);

        for (int i = 0; i < m_iTotalCell; ++i)
            m_arrCellCount[i] = 0;

        m_iItemCount = 0;
    }

    public void AddItem(int _iDenseIndex, Vector3 _vWorldPos)
    {
        if (m_bBuilt == false)
            return;

        ComputeCellCoord(_vWorldPos, m_vOrigin, m_fCellSize, m_iCountX, m_iCountY, m_iCountZ,
            out int iX, out int iY, out int iZ);

        int iCell = FlattenIndex(iX, iY, iZ, m_iCountX, m_iCountY);

        m_arrScratchCellIndexPerItem[_iDenseIndex] = iCell;
        m_arrCellCount[iCell] = m_arrCellCount[iCell] + 1;

        if (_iDenseIndex + 1 > m_iItemCount)
            m_iItemCount = _iDenseIndex + 1;
    }

    public void EndRebuild()
    {
        if (m_bBuilt == false)
            return;

        int iRunning = 0;
        for (int i = 0; i < m_iTotalCell; ++i)
        {
            m_arrCellStart[i] = iRunning;
            m_arrScratchCursor[i] = iRunning;
            iRunning += m_arrCellCount[i];
        }

        for (int i = 0; i < m_iItemCount; ++i)
        {
            int iCell = m_arrScratchCellIndexPerItem[i];
            int iSlot = m_arrScratchCursor[iCell];
            m_arrCellItems[iSlot] = i;
            m_arrScratchCursor[iCell] = iSlot + 1;
        }
    }

    public void Dispose()
    {
        DisposeCellArrays();

        if (m_arrCellItems.IsCreated)
            m_arrCellItems.Dispose();
        if (m_arrScratchCellIndexPerItem.IsCreated)
            m_arrScratchCellIndexPerItem.Dispose();

        m_bBuilt = false;
        m_iItemCount = 0;
    }

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

    private void EnsureItemCapacity(int _iCount)
    {
        if (m_arrCellItems.IsCreated && m_arrCellItems.Length >= _iCount)
            return;

        int iNewCapacity = m_arrCellItems.IsCreated ? m_arrCellItems.Length : MIN_ITEM_CAPACITY;
        while (iNewCapacity < _iCount)
            iNewCapacity <<= 1;

        if (m_arrCellItems.IsCreated)
            m_arrCellItems.Dispose();
        if (m_arrScratchCellIndexPerItem.IsCreated)
            m_arrScratchCellIndexPerItem.Dispose();

        m_arrCellItems = new NativeArray<int>(iNewCapacity, Allocator.Persistent);
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
