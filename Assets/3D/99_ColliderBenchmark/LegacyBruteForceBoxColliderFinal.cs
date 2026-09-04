using UnityEngine;

/*///////////////////////////////////////////
         LegacyBruteForceBoxColliderFinal
목적 : 챕터 4 전용 Box 콜라이더(장애물). 정적이라 TransformAccessArray에 등록되지
       않고, Start()에서 계산한 CachedCenter/축을 그대로 유지한다.
 *///////////////////////////////////////////
public sealed class LegacyBruteForceBoxColliderFinal : LegacyBruteForceColliderFinal
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
        Shape = eLegacyColliderShapeFinal.Box;
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
