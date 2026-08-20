using UnityEngine;

/*///////////////////////////////////////////
                ObbCollider
목적 : PhysX Collider에 의존하지 않는 자체 OBB(회전된 직육면체) 충돌 판정용 컴포넌트.

       크기는 CircleCollider의 m_fRadius와 동일하게 인스펙터에서 직접 설정한다 -
       메시 sharedMesh.bounds는 여러 변형이 하나의 메시 에셋에 뭉쳐있거나 임포트 시점
       바운드가 부정확한 경우가 있어 신뢰할 수 없었음(기즈모로 실제 눈으로 확인됨).

       계약: 위치/회전은 매 프레임 갱신된다(RefreshCenter, CircleCollider와 동일
       패턴) - Box 콜라이더가 정적이라고 가정하지 않는다(BoxColliderGrid도 이동을
       전제로 설계됨). 다만 half-extent/BoundingRadius(스케일 기반)는 Start()에서
       한 번만 계산하고 이후 갱신하지 않는다 - 런타임에 스케일이 바뀌는 Box가
       필요해지면 이 계산도 RefreshCenter로 옮겨야 함(현재 범위 밖).
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

    // m_vAxisX/Y/Z는 RefreshCenter()에서 매 프레임 갱신됨. m_vHalfExtent는 스케일 기반이라
    // Start()에서 한 번만 계산(런타임 스케일 변경 미지원, 클래스 주석 참고)
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

        // 스케일 기반 값은 여기서 한 번만 계산(런타임 스케일 변경은 미지원, 클래스 주석 참고)
        Vector3 vLossyScale = transform.lossyScale;
        m_vHalfExtent = new Vector3(
            m_vBaseHalfExtent.x * Mathf.Abs(vLossyScale.x),
            m_vBaseHalfExtent.y * Mathf.Abs(vLossyScale.y),
            m_vBaseHalfExtent.z * Mathf.Abs(vLossyScale.z));
        BoundingRadius = m_vHalfExtent.magnitude;

        // 중심/축 초기값은 RefreshCenter와 동일 계산이라 중복 없이 그대로 재사용
        RefreshCenter();
    }

    // 위치/축을 매 프레임 실제로 갱신한다(CircleCollider.RefreshCenter와 동일 패턴) -
    // Box 콜라이더가 정적이라고 가정하지 않으므로 이동/회전이 즉시 반영돼야 함
    public override void RefreshCenter()
    {
        CachedCenter = transform.position + transform.rotation * m_vOffset;
        m_vAxisX = transform.right;
        m_vAxisY = transform.up;
        m_vAxisZ = transform.forward;
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
