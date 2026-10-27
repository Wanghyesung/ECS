using UnityEngine;

// 자체 구 충돌체의 반지름과 중심을 제공한다.

public class CircleCollider : BaseCollider
{
    [SerializeField] private float m_fRadius = 0.5f;
    // 로컬 중심 보정값.
    [SerializeField] private Vector3 m_vOffset = Vector3.zero;

    [Header("Debug")]
    [SerializeField] private bool m_bShowDebugGizmo = false;
    [SerializeField] private Color m_tGizmoColor = Color.green;

    public float Radius => m_fRadius;
    public override float BoundingRadius => m_fRadius;

    // 발사 시 WeaponCon이 최종 반경을 적용하고 Bullet.OnDisable이 풀 반납 시 원복한다.
    public void SetRadius(float _fRadius) => m_fRadius = _fRadius;
    public override Vector3 Offset => m_vOffset;

    // 회전까지 반영된 실제 판정 중심 (오프셋이 0이면 transform.position과 동일)
    public Vector3 Center => transform.position + transform.rotation * m_vOffset;

    protected override void Awake()
    {
        base.Awake();
        Shape = eColliderShape.Circle;
    }

    // 테스트 등에서 판정용 중심을 동기 갱신한다.
    public override void RefreshCenter()
    {
        CachedCenter = transform.position + transform.rotation * m_vOffset;
    }

    private void OnDrawGizmos()
    {
        if (m_bShowDebugGizmo == false)
            return;

        Gizmos.color = m_tGizmoColor;
        // 캐시 갱신 전에도 현재 위치에 표시한다.
        Gizmos.DrawWireSphere(Center, m_fRadius);
    }
}
