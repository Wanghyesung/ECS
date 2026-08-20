using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
              BoxColliderGrid
목적 : Shape==Box인 콜라이더 그룹 대상 정적 공간분할 그리드. 셀마다 List<BaseCollider>를
       Build 시점에 전부 미리 만들어두고(그리드 범위가 정적으로 확정되므로 가능),
       런타임에는 절대 새 List를 만들지 않는다 - Dictionary 해싱도 없고, 다른 ID를
       쫓아가는 연결 리스트 방식(포인터 추적)도 아니고, 그냥 플랫 배열 인덱스로 바로
       그 셀의 List에 접근한다.

       [,,] 다차원 배열이 아니라 1차원 배열 + 수동 플래튼(x + y*W + z*W*H)을 쓰는 이유:
       C#/.NET에서 다차원 배열 인덱싱이 1차원 배열보다 느린 게 잘 알려진 특성이라,
       "배열로 바로 접근"이라는 목적엔 1차원 쪽이 더 낫다.

       그리드 범위(셀 개수/원점)는 Build 시점에 넘겨받은 Box 콜라이더들의 실제 분포
       (CachedCenter ± BoundingRadius의 합집합)로 딱 한 번 정해진다("정적 분할") -
       인스펙터로 따로 설정하지 않는다. 이후 콜라이더 개별 위치가 바뀌어도(정적이
       아닌 Box) UpdateCell로 셀 소속만 갱신되고, 그리드 배열 자체의 크기/원점은
       다시 계산하지 않는다. 그리드 범위 밖 위치는 가장 가까운 가장자리 셀로
       클램프된다(경계 근처만 정밀도가 살짝 낮아질 뿐, 놓치는 쌍은 없음).

       콜라이더 하나는 정확히 셀 하나에만 속해서(자기 중심이 속한 셀), 이웃 셀들이
       서로 겹치는 원소가 없어 조회 시 별도 중복 제거가 필요 없다. 이웃 1겹
       (3×3×3=27칸)만으로 실제 겹침을 놓치지 않으려면 셀 크기가 "겹칠 수 있는 두
       콜라이더 반지름의 합"보다 커야 한다는 전제가 있음 - Build가 그 시점 최대
       BoundingRadius 기준으로 셀 크기를 계산해서 책임진다.
 *///////////////////////////////////////////
public sealed class BoxColliderGrid
{
    private const float MIN_CELL_SIZE = 1f;
    private const int EMPTY = -1;

    private List<BaseCollider>[] m_arrCell;                                   // 플랫 셀 인덱스 -> 그 셀의 콜라이더 목록(Build 시 전부 미리 생성)
    private readonly List<int> m_listColliderCellIndex = new List<int>();     // ID -> 현재 소속 셀의 플랫 인덱스(없으면 EMPTY)

    private Vector3 m_vOrigin;
    private float m_fCellSize = MIN_CELL_SIZE;
    private int m_iCountX;
    private int m_iCountY;
    private int m_iCountZ;
    private bool m_bBuilt;

    public bool IsBuilt => m_bBuilt;

    // Box 콜라이더 목록의 실제 분포로 그리드 범위를 딱 한 번 정한다("정적 분할").
    // 셀마다 List<BaseCollider>를 여기서 전부 미리 만들어서, 런타임 중엔 신규 List
    // 할당이 절대 안 일어나게 한다. 셀 크기는 그 시점 최대 BoundingRadius*2 -
    // 이웃 1겹 검사만으로 놓치지 않기 위한 하한
    public void Build(List<BaseCollider> _listBoxCollider)
    {
        m_bBuilt = false;
        if (_listBoxCollider == null || _listBoxCollider.Count == 0)
            return;

        Vector3 vMin = _listBoxCollider[0].CachedCenter;
        Vector3 vMax = vMin;
        float fMaxRadius = 0f;

        for (int i = 0; i < _listBoxCollider.Count; ++i)
        {
            BaseCollider refCollider = _listBoxCollider[i];
            float fRadius = ((ObbCollider)refCollider).BoundingRadius;
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

        int iTotalCells = m_iCountX * m_iCountY * m_iCountZ;
        m_arrCell = new List<BaseCollider>[iTotalCells];
        for (int i = 0; i < iTotalCells; ++i)
            m_arrCell[i] = new List<BaseCollider>();

        // 배열이 새로 지어졌으니 이전에 알던 소속 기록은 전부 무효 - 호출부가
        // Build 직후 대상 콜라이더마다 UpdateCell을 다시 불러 재등록해야 함
        for (int i = 0; i < m_listColliderCellIndex.Count; ++i)
            m_listColliderCellIndex[i] = EMPTY;

        m_bBuilt = true;
    }

    // 무할당 삽입/이동 - 셀이 안 바뀐 대다수는 인덱스 계산 + 비교 1회로 끝남
    public void UpdateCell(BaseCollider _refBoxCollider)
    {
        if (!m_bBuilt)
            return;

        int iID = _refBoxCollider.ID;
        RegisterColliderIndex(iID);

        int iNewCellIndex = ComputeCellIndex(_refBoxCollider.CachedCenter);
        int iOldCellIndex = m_listColliderCellIndex[iID];
        if (iNewCellIndex == iOldCellIndex)
            return;

        if (iOldCellIndex != EMPTY)
            RemoveCellList(m_arrCell[iOldCellIndex], _refBoxCollider);

        m_arrCell[iNewCellIndex].Add(_refBoxCollider);
        m_listColliderCellIndex[iID] = iNewCellIndex;
    }

    // 범위 밖으로 빠졌거나(플레이어 거리 컬링) 파괴/풀 반납된 콜라이더를 그리드에서 뺀다
    public void RemoveCollider(BaseCollider _refBoxCollider)
    {
        int iID = _refBoxCollider.ID;
        if (iID >= m_listColliderCellIndex.Count)
            return;

        int iCellIndex = m_listColliderCellIndex[iID];
        if (iCellIndex == EMPTY)
            return;

        RemoveCellList(m_arrCell[iCellIndex], _refBoxCollider);
        m_listColliderCellIndex[iID] = EMPTY;
    }

    // 지금 그리드에 들어있는 상태인지 - Static 콜라이더가 이미 등록됐으면 재계산을
    // 건너뛰기 위해 ColliderManager.RefreshBoxGrids가 확인용으로 사용
    public bool Contains(BaseCollider _refBoxCollider)
    {
        int iID = _refBoxCollider.ID;
        return iID < m_listColliderCellIndex.Count && m_listColliderCellIndex[iID] != EMPTY;
    }

    // 무할당 - 총알 수만큼 매 프레임 다수 호출. 콜라이더가 셀 하나에만 속하므로 중복 없음.
    // 각 셀의 List<BaseCollider>를 그대로 순회하므로(ID를 거쳐 따로 찾아가지 않음) 그
    // 셀 안에서는 연속 메모리 접근
    public void NeighborColliders(Vector3 _vCenter, List<BaseCollider> _listResult)
    {
        _listResult.Clear();
        if (!m_bBuilt)
            return;

        ComputeCellCoord(_vCenter, out int iCX, out int iCY, out int iCZ);

        int iMinX = Mathf.Max(0, iCX - 1);
        int iMaxX = Mathf.Min(m_iCountX - 1, iCX + 1);
        int iMinY = Mathf.Max(0, iCY - 1);
        int iMaxY = Mathf.Min(m_iCountY - 1, iCY + 1);
        int iMinZ = Mathf.Max(0, iCZ - 1);
        int iMaxZ = Mathf.Min(m_iCountZ - 1, iCZ + 1);

        for (int ix = iMinX; ix <= iMaxX; ++ix)
        for (int iy = iMinY; iy <= iMaxY; ++iy)
        for (int iz = iMinZ; iz <= iMaxZ; ++iz)
        {
            List<BaseCollider> listCell = m_arrCell[FlattenIndex(ix, iy, iz)];
            for (int i = 0; i < listCell.Count; ++i)
                _listResult.Add(listCell[i]);
        }
    }

    // 스왑백 제거(O(1)) - ColliderManager.DeleteCollider의 레이어 리스트 제거와 동일 관례.
    // 셀 안 순서는 의미가 없으므로 맨 뒤 원소를 빈자리로 옮기고 자름
    private static void RemoveCellList(List<BaseCollider> _listCell, BaseCollider _refCollider)
    {
        int iIndex = _listCell.IndexOf(_refCollider);
        if (iIndex < 0)
            return;

        int iLastIndex = _listCell.Count - 1;
        _listCell[iIndex] = _listCell[iLastIndex];
        _listCell.RemoveAt(iLastIndex);
    }

    private void RegisterColliderIndex(int _iID)
    {
        while (m_listColliderCellIndex.Count <= _iID)
            m_listColliderCellIndex.Add(EMPTY);
    }

    private int ComputeCellIndex(Vector3 _vWorldPos)
    {
        ComputeCellCoord(_vWorldPos, out int iX, out int iY, out int iZ);
        return FlattenIndex(iX, iY, iZ);
    }

    // 그리드 범위 밖 위치는 가장 가까운 가장자리 셀로 클램프한다
    private void ComputeCellCoord(Vector3 _vWorldPos, out int _iX, out int _iY, out int _iZ)
    {
        Vector3 vLocal = _vWorldPos - m_vOrigin;
        _iX = Mathf.Clamp(Mathf.FloorToInt(vLocal.x / m_fCellSize), 0, m_iCountX - 1);
        _iY = Mathf.Clamp(Mathf.FloorToInt(vLocal.y / m_fCellSize), 0, m_iCountY - 1);
        _iZ = Mathf.Clamp(Mathf.FloorToInt(vLocal.z / m_fCellSize), 0, m_iCountZ - 1);
    }

    private int FlattenIndex(int _iX, int _iY, int _iZ)
    {
        return (_iZ * m_iCountY + _iY) * m_iCountX + _iX;
    }
}
