using UnityEngine;

// 자체 회전 박스 충돌체. 크기는 Start에서 고정하고 위치와 축은 Job으로 갱신한다.

public class ObbCollider : BaseCollider
{
    // 원본(스케일 1 기준) half-extent. 인스턴스마다 lossyScale이 곱해져 실제 크기가 됨
    [SerializeField] private Vector3 m_vBaseHalfExtent = Vector3.one * 0.5f;
    // 로컬 중심 보정값.
    [SerializeField] private Vector3 m_vOffset = Vector3.zero;

    [Header("Debug")]
    [SerializeField] private bool m_bShowDebugGizmo = false;
    [SerializeField] private Color m_tGizmoColor = Color.cyan;

    // 축은 Job으로 갱신하고 크기는 Start에서 고정한다.
    private Vector3 m_vAxisX;
    private Vector3 m_vAxisY;
    private Vector3 m_vAxisZ;
    private Vector3 m_vHalfExtent;

    // 박스를 감싸는 구의 반지름. 정밀 판정 전 거리 검사에 사용한다.
    private float m_fBoundingRadius;
    public override float BoundingRadius => m_fBoundingRadius;
    public override Vector3 Offset => m_vOffset;

    public Vector3 AxisX => m_vAxisX;
    public Vector3 AxisY => m_vAxisY;
    public Vector3 AxisZ => m_vAxisZ;
    public Vector3 HalfExtent => m_vHalfExtent;

    protected override void Awake()
    {
        base.Awake();
        Shape = eColliderShape.Box;
    }

    protected override void Start()
    {
        base.Start();

        // 스케일 기반 값은 여기서 한 번만 계산(런타임 스케일 변경은 미지원, 클래스 주석 참고)
        Vector3 vLossyScale = transform.lossyScale;
        m_vHalfExtent = new Vector3(
            m_vBaseHalfExtent.x * Mathf.Abs(vLossyScale.x),
            m_vBaseHalfExtent.y * Mathf.Abs(vLossyScale.y),
            m_vBaseHalfExtent.z * Mathf.Abs(vLossyScale.z));
        m_fBoundingRadius = m_vHalfExtent.magnitude;

        // 중심/축 초기값은 RefreshCenter와 동일 계산이라 중복 없이 그대로 재사용
        RefreshCenter();
    }

    // 초기화용 동기 갱신. 이후에는 Job 결과를 적용한다.
    public override void RefreshCenter()
    {
        CachedCenter = transform.position + transform.rotation * m_vOffset;
        m_vAxisX = transform.right;
        m_vAxisY = transform.up;
        m_vAxisZ = transform.forward;
    }

    // ColliderManager.PreLoadCenter()가 RefreshCenterJob 결과를 되돌려 쓸 때만 호출
    public void ApplyAxis(Vector3 _vAxisX, Vector3 _vAxisY, Vector3 _vAxisZ)
    {
        m_vAxisX = _vAxisX;
        m_vAxisY = _vAxisY;
        m_vAxisZ = _vAxisZ;
    }

    private void OnDrawGizmos()
    {
        if (m_bShowDebugGizmo == false)
            return;

        Gizmos.color = m_tGizmoColor;
        Matrix4x4 tOldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(m_vOffset, m_vHalfExtent * 2f);
        Gizmos.matrix = tOldMatrix;
    }
}
