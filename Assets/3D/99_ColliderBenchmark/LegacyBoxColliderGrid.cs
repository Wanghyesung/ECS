using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
              LegacyBoxColliderGrid
목적 : 커밋 8edea44(Box 콜라이더 브로드페이즈를 정적 공간분할 그리드로 교체) 시점의
       설계를 그대로 옮긴 벤치마크 전용 그리드 - Box(Obstacle) 레이어만 담당한다.
       Circle-Circle(총알↔몬스터)은 이 단계에서 아직 브루트포스로 남아있는 게
       역사적으로 정확하다(Circle-Circle까지 그리드로 통합되는 건 훨씬 뒤 44b4646).

       현재 프로덕션 BoxColliderGrid(Assets/3D/05_Manager)와 달리 NativeArray/Burst가
       전혀 없다 - Job화(daef9e3)는 바로 다음 단계라 아직 등장하면 안 된다. 셀 내용은
       그냥 List<LegacyBruteForceCollider> 사전할당 배열(Docs/Collider.md §2-4 "4차
       설계"의 최종 형태: Dictionary 제거 → 셀별 List 사전할당).

       콜라이더 하나는 자기 중심이 속한 셀 하나에만 등록되고, 조회 측은 이웃 27칸만
       본다 - 이웃 1겹만으로 놓치지 않으려면 셀 크기가 "겹칠 수 있는 두 콜라이더
       반지름의 합"보다 커야 한다는 전제가 있고, Build가 그 시점 최대
       BoundingRadius*2로 셀 크기를 잡아 그걸 책임진다(BoxColliderGrid.cs와 동일 근거).

       그리드 범위(셀 개수/원점/셀 크기)는 Build 시점에 한 번만 정해진다("정적 분할").
       매 프레임 바뀌는 건 셀 소속(멤버십)뿐이고, 그건 BeginRebuild→AddCollider로
       매번 통째로 다시 채운다(장애물이 이 벤치마크에서는 정적이라 사실상 저비용).
 *///////////////////////////////////////////
public sealed class LegacyBoxColliderGrid
{
    private const float MIN_CELL_SIZE = 1f;

    private List<LegacyBruteForceCollider>[] m_arrCell;
    private Vector3 m_vOrigin;
    private float m_fCellSize = MIN_CELL_SIZE;
    private int m_iCountX = 1;
    private int m_iCountY = 1;
    private int m_iCountZ = 1;
    private bool m_bBuilt;

    public bool IsBuilt => m_bBuilt;
    public Vector3 Origin => m_vOrigin;
    public float CellSize => m_fCellSize;
    public int CountX => m_iCountX;
    public int CountY => m_iCountY;
    public int CountZ => m_iCountZ;

    // 이 그리드를 소유한 레이어(Box 콜라이더 목록)의 실제 분포로 그리드 범위를 딱 한 번 정한다
    public void Build(List<LegacyBruteForceCollider> _listOwnerCollider)
    {
        m_bBuilt = false;
        if (_listOwnerCollider == null || _listOwnerCollider.Count == 0)
            return;

        Vector3 vMin = _listOwnerCollider[0].CachedCenter;
        Vector3 vMax = vMin;
        float fMaxRadius = 0f;

        for (int i = 0; i < _listOwnerCollider.Count; ++i)
        {
            LegacyBruteForceCollider refCollider = _listOwnerCollider[i];
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

        int iTotalCell = m_iCountX * m_iCountY * m_iCountZ;
        m_arrCell = new List<LegacyBruteForceCollider>[iTotalCell];
        for (int i = 0; i < iTotalCell; ++i)
            m_arrCell[i] = new List<LegacyBruteForceCollider>();

        m_bBuilt = true;
    }

    // 매 프레임 멤버십만 통째로 다시 채운다 - 셀별 List는 Clear만 하고 용량은 유지
    public void BeginRebuild()
    {
        if (m_bBuilt == false)
            return;

        for (int i = 0; i < m_arrCell.Length; ++i)
            m_arrCell[i].Clear();
    }

    public void AddCollider(LegacyBruteForceCollider _refCollider)
    {
        if (m_bBuilt == false)
            return;

        ComputeCellCoord(_refCollider.CachedCenter, m_vOrigin, m_fCellSize, m_iCountX, m_iCountY, m_iCountZ,
            out int iX, out int iY, out int iZ);

        m_arrCell[FlattenIndex(iX, iY, iZ, m_iCountX, m_iCountY)].Add(_refCollider);
    }

    public List<LegacyBruteForceCollider> GetCell(int _iX, int _iY, int _iZ)
    {
        return m_arrCell[FlattenIndex(_iX, _iY, _iZ, m_iCountX, m_iCountY)];
    }

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
}
