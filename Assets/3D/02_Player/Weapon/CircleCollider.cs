using UnityEngine;

/*///////////////////////////////////////////
              CircleCollider
목적 : PhysX Collider/OnTriggerEnter에 의존하지 않는 자체 원(구) 충돌 판정용 컴포넌트.
       ColliderManager가 관리하는 그리드에 등록되어, 인접 셀의 다른 콜라이더와
       거리 비교로 겹침을 판정한다.

       PhysX Collider를 완전히 걷어내는 게 목적이라, 이벤트 payload도 UnityEngine.Collider가
       아니라 BaseCollider 자기 자신을 그대로 넘긴다. 그래서 ITriggerable은 구현하지 않고,
       Bullet/Missiles/GuidedBullet/AttackObject가 BaseCollider를 직접 참조하는 구조로 간다.

       ID/레이어 캐싱, Activate/UnActivate 생애주기, Enter/Stay/Exit 이벤트는 BaseCollider가
       공통으로 구현하고 있음 - 여기서는 원(구) 고유 데이터(반지름/오프셋)와 매 프레임
       중심 갱신만 담당한다.
 *///////////////////////////////////////////

public class CircleCollider : BaseCollider
{
    [SerializeField] private float m_fRadius = 0.5f;
    // 모델 피벗이 실제 판정 중심과 다를 때(예: 몬스터 피벗이 발밑) 로컬 공간 기준으로 보정
    [SerializeField] private Vector3 m_vOffset = Vector3.zero;

    [Header("Debug")]
    [SerializeField] private bool m_bShowDebugGizmo = false;
    [SerializeField] private Color m_tGizmoColor = Color.green;

    public float Radius => m_fRadius;
    public override float BoundingRadius => m_fRadius;
    public override Vector3 Offset => m_vOffset;

    // 회전까지 반영된 실제 판정 중심 (오프셋이 0이면 transform.position과 동일)
    public Vector3 Center => transform.position + transform.rotation * m_vOffset;

    protected override void Awake()
    {
        base.Awake();
        Shape = eColliderShape.Circle;
    }

    // Center는 쿼터니언 곱셈이 들어있어 쌍마다 반복 조회하면 낭비가 크다(총알 하나가 몬스터
    // 수만큼, 몬스터 하나가 총알 수만큼 매번 다시 계산됨) - 그래서 CachedCenter로 캐싱해두고
    // 판정은 그 값만 읽는다. 매 프레임 갱신은 더 이상 이 메서드가 아니라 ColliderManager의
    // RefreshCenterJob(TransformAccessArray 병렬 처리) + ApplyCachedCenter가 담당한다.
    // 이 메서드 자체는 씬 없이 EditMode 테스트에서 동기적으로 값을 채울 때 등에 여전히 유효
    public override void RefreshCenter()
    {
        CachedCenter = transform.position + transform.rotation * m_vOffset;
    }

    private void OnDrawGizmos()
    {
        if (m_bShowDebugGizmo == false)
            return;

        Gizmos.color = m_tGizmoColor;
        // 실제 판정에 쓰이는 값(CachedCenter)과 같은 계산식(Center)을 그대로 그려서, 눈에 보이는 원이랑
        // 진짜 판정용 원이 다른 자리에 있는 건 아닌지 바로 비교 가능하게 함
        Gizmos.DrawWireSphere(Center, m_fRadius);
    }
}
