using UnityEngine;

/*///////////////////////////////////////////
           LegacyBruteForceBoxColliderJob
목적 : daef9e3 단계 전용 Box 콜라이더(장애물). GridTestScene0의
       LegacyBruteForceBoxCollider와 로직은 동일하고 베이스 타입만 Job 버전이다 -
       정적 오브젝트 전제(Start에서 축/half-extent를 한 번만 계산)도 그대로.
 *///////////////////////////////////////////
public sealed class LegacyBruteForceBoxColliderJob : LegacyBruteForceColliderJob
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
        Shape = eLegacyColliderShapeJob.Box;
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
