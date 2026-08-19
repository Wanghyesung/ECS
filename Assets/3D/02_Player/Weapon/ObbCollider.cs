using UnityEngine;

/*///////////////////////////////////////////
                ObbCollider
목적 : PhysX Collider에 의존하지 않는 자체 OBB(회전된 직육면체) 충돌 판정용 컴포넌트.
       운석처럼 스폰/파괴 없이 씬 시작 시 고정 배치되는 정적 장애물 전용 - 그래서
       CircleCollider와 달리 중심/축/half-extent를 Start()에서 딱 한 번만 계산해
       캐싱하고, 이후 매 프레임 갱신(RefreshCenter)은 빈 구현으로 둔다.

       크기는 CircleCollider의 m_fRadius와 동일하게 인스펙터에서 직접 설정한다 -
       메시 sharedMesh.bounds는 여러 변형이 하나의 메시 에셋에 뭉쳐있거나 임포트 시점
       바운드가 부정확한 경우가 있어 신뢰할 수 없었음(기즈모로 실제 눈으로 확인됨).

       계약(중요): 이 컴포넌트는 정적 오브젝트 전용이다. Start() 이후 transform을
       옮기거나 회전시켜도 CachedCenter/축/half-extent는 갱신되지 않는다(Obstacle
       레이어에 공간 그리드 등 정적 전제의 브로드페이즈 최적화가 들어갈 예정이라 이
       제약이 더 굳어질 것). 움직이는 박스 장애물이 필요해지면 이 클래스를 재사용하지
       말고 별도 타입을 만들 것.
 *///////////////////////////////////////////

public class ObbCollider : BaseCollider
{
    // 원본(스케일 1 기준) half-extent. 인스턴스마다 lossyScale이 곱해져 실제 크기가 됨
    [SerializeField] private Vector3 m_vBaseHalfExtent = Vector3.one * 0.5f;
    // 모델 피벗이 실제 판정 중심과 다를 때(예: 피벗이 발밑) 로컬 공간 기준으로 보정.
    // CircleCollider의 m_vOffset과 동일한 역할
    [SerializeField] private Vector3 m_vOffset = Vector3.zero;

    [Header("Debug")]
    [SerializeField] private bool m_bShowDebugGizmo = false;
    [SerializeField] private Color m_tGizmoColor = Color.cyan;

    // Start()에서 한 번만 계산해 캐싱 - 정적 오브젝트라 이후 다시 계산하지 않음
    private Vector3 m_vAxisX;
    private Vector3 m_vAxisY;
    private Vector3 m_vAxisZ;
    private Vector3 m_vHalfExtent;

    // half-extent 대각선 길이 - 이 반지름의 구보다 멀리 있으면 박스와 절대 안 겹친다는
    // 보수적 상한. Start()에서 한 번만 계산해서, 원-박스 판정마다 비싼 OBB 수학(내적 3회
    // + 클램프)을 돌리기 전에 값싼 구-구 선판정으로 명백히 먼 쌍을 미리 걸러내는 용도
    public float BoundingRadius { get; private set; }

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

        m_vAxisX = transform.right;
        m_vAxisY = transform.up;
        m_vAxisZ = transform.forward;

        Vector3 vLossyScale = transform.lossyScale;
        m_vHalfExtent = new Vector3(
            m_vBaseHalfExtent.x * Mathf.Abs(vLossyScale.x),
            m_vBaseHalfExtent.y * Mathf.Abs(vLossyScale.y),
            m_vBaseHalfExtent.z * Mathf.Abs(vLossyScale.z));

        CachedCenter = transform.position + transform.rotation * m_vOffset;
        BoundingRadius = m_vHalfExtent.magnitude;
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
