using UnityEngine;

/*///////////////////////////////////////////
           LegacyBruteForceBoxCollider
목적 : 421d1b0 시점 ObbCollider 포팅 - 장애물 전용 OBB 콜라이더. 정적 오브젝트
       전제(Start에서 축/half-extent를 한 번만 계산하고 이후 갱신하지 않음)도
       원본과 동일하다.
 *///////////////////////////////////////////
public sealed class LegacyBruteForceBoxCollider : LegacyBruteForceCollider
{
    [SerializeField] private Vector3 m_vHalfExtent = Vector3.one * 0.5f;

    private Vector3 m_vAxisX;
    private Vector3 m_vAxisY;
    private Vector3 m_vAxisZ;
    private float m_fBoundingRadius;

    // half-extent 대각선 길이 - 원-박스 판정 전 값싼 구-구 선판정용 보수적 상한.
    // base가 get-only virtual이라 override에서 set accessor를 새로 추가할 수 없어
    // 백킹 필드로 둔다(ObbCollider.cs와 동일 패턴)
    public override float BoundingRadius => m_fBoundingRadius;
    public Vector3 AxisX => m_vAxisX;
    public Vector3 AxisY => m_vAxisY;
    public Vector3 AxisZ => m_vAxisZ;
    public Vector3 HalfExtent => m_vHalfExtent;

    public void SetHalfExtent(Vector3 _vHalfExtent) => m_vHalfExtent = _vHalfExtent;

    protected override void Awake()
    {
        base.Awake();
        Shape = eLegacyColliderShape.Box;
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
