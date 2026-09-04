using UnityEngine;

/*///////////////////////////////////////////
        LegacyBruteForceBoxColliderUnified
목적 : 챕터 3 전용 Box 콜라이더(장애물). 정적 오브젝트 전제(Start에서 축/half-extent를
       한 번만 계산)는 계속 유지된다.
 *///////////////////////////////////////////
public sealed class LegacyBruteForceBoxColliderUnified : LegacyBruteForceColliderUnified
{
    [SerializeField] private Vector3 m_vHalfExtent = Vector3.one * 0.5f;

    private Vector3 m_vAxisX;
    private Vector3 m_vAxisY;
    private Vector3 m_vAxisZ;
    private float m_fBoundingRadius;

    public override float BoundingRadius => m_fBoundingRadius;
    public Vector3 AxisX => m_vAxisX;
    public Vector3 AxisY => m_vAxisY;
    public Vector3 AxisZ => m_vAxisZ;
    public Vector3 HalfExtent => m_vHalfExtent;

    public void SetHalfExtent(Vector3 _vHalfExtent) => m_vHalfExtent = _vHalfExtent;

    protected override void Awake()
    {
        base.Awake();
        Shape = eLegacyColliderShapeUnified.Box;
    }

    protected override void Start()
    {
        base.Start();

        m_vAxisX = transform.right;
        m_vAxisY = transform.up;
        m_vAxisZ = transform.forward;

        CachedCenter = transform.position;
        m_fBoundingRadius = m_vHalfExtent.magnitude;
    }
}
