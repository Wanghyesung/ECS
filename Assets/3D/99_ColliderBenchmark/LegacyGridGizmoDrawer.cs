using UnityEngine;

/*///////////////////////////////////////////
                LegacyGridGizmoDrawer
목적 : GridTestScene3(챕터 4 최종 재현) Play 모드에서 그리드 셀 경계와 콜라이더를
       Scene 뷰에 Gizmos로 겹쳐 그려서, 포트폴리오용 "그리드 시각화" 캡처를 찍을 수
       있게 한다. 판정 로직에는 전혀 관여하지 않는 순수 디버그 전용 컴포넌트 -
       LegacyBruteForceColliderManagerFinal.Grid(읽기 전용)와 씬에 존재하는
       LegacyBruteForceColliderFinal들을 조회만 한다.
 *///////////////////////////////////////////
public sealed class LegacyGridGizmoDrawer : MonoBehaviour
{
    [Header("표시 대상")]
    [SerializeField] private bool m_bDrawCells = true;
    [SerializeField] private bool m_bDrawColliders = true;

    [Header("색상")]
    [SerializeField] private Color m_tCellColor = new Color(1f, 0.64f, 0.24f, 0.6f);
    [SerializeField] private Color m_tBulletColor = new Color(0.35f, 0.85f, 1f, 0.8f);
    [SerializeField] private Color m_tMonsterColor = new Color(1f, 0.25f, 0.2f, 0.9f);
    [SerializeField] private Color m_tObstacleColor = new Color(0.7f, 0.7f, 0.7f, 0.7f);

    private void OnDrawGizmos()
    {
        LegacyBruteForceColliderManagerFinal refManager = LegacyBruteForceColliderManagerFinal.Instance;
        if (refManager == null)
            return;

        LegacyColliderGridFinal refGrid = refManager.Grid;
        if (refGrid == null || refGrid.IsBuilt == false)
            return;

        if (m_bDrawCells)
            DrawOccupiedCells(refGrid);

        if (m_bDrawColliders)
            DrawColliders();
    }

    private void DrawOccupiedCells(LegacyColliderGridFinal _refGrid)
    {
        Gizmos.color = m_tCellColor;

        Vector3 vCellSize = Vector3.one * _refGrid.CellSize;
        Vector3 vHalfCell = vCellSize * 0.5f;

        int iCountX = _refGrid.CountX;
        int iCountY = _refGrid.CountY;
        int iCountZ = _refGrid.CountZ;

        for (int ix = 0; ix < iCountX; ++ix)
        {
            for (int iy = 0; iy < iCountY; ++iy)
            {
                for (int iz = 0; iz < iCountZ; ++iz)
                {
                    int iCell = LegacyColliderGridFinal.FlattenIndex(ix, iy, iz, iCountX, iCountY);
                    if (_refGrid.CellCount[iCell] <= 0)
                        continue;

                    Vector3 vCellMin = _refGrid.Origin + new Vector3(ix, iy, iz) * _refGrid.CellSize;
                    Gizmos.DrawWireCube(vCellMin + vHalfCell, vCellSize);
                }
            }
        }
    }

    private void DrawColliders()
    {
        LegacyBruteForceColliderFinal[] arrCollider = FindObjectsByType<LegacyBruteForceColliderFinal>(FindObjectsSortMode.None);

        for (int i = 0; i < arrCollider.Length; ++i)
        {
            LegacyBruteForceColliderFinal refCollider = arrCollider[i];
            string strLayerName = LayerMask.LayerToName(refCollider.Layer);

            if (refCollider.Shape == eLegacyColliderShapeFinal.Box)
            {
                DrawBox((LegacyBruteForceBoxColliderFinal)refCollider);
                continue;
            }

            Gizmos.color = strLayerName == "PhysXMonsterDemo" ? m_tMonsterColor : m_tBulletColor;
            Gizmos.DrawWireSphere(refCollider.CachedCenter, refCollider.BoundingRadius);
        }
    }

    private void DrawBox(LegacyBruteForceBoxColliderFinal _refBox)
    {
        Matrix4x4 tRotationMatrix = new Matrix4x4(
            _refBox.AxisX, _refBox.AxisY, _refBox.AxisZ, new Vector4(0f, 0f, 0f, 1f));

        Gizmos.color = m_tObstacleColor;
        Gizmos.matrix = Matrix4x4.Translate(_refBox.CachedCenter) * tRotationMatrix;
        Gizmos.DrawWireCube(Vector3.zero, _refBox.HalfExtent * 2f);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
