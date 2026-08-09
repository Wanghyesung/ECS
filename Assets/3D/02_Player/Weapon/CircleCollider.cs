using System;
using UnityEngine;

/*///////////////////////////////////////////
              CircleCollider
목적 : PhysX Collider/OnTriggerEnter에 의존하지 않는 자체 원(구) 충돌 판정용 컴포넌트.
       ColliderManager가 관리하는 그리드에 등록되어, 인접 셀의 다른 CircleCollider와
       거리 비교로 겹침을 판정한다.

       PhysX Collider를 완전히 걷어내는 게 목적이라, 이벤트 payload도 UnityEngine.Collider가
       아니라 CircleCollider 자기 자신을 그대로 넘긴다. 그래서 ITriggerable은 구현하지 않고,
       Bullet/Missiles/GuidedBullet/AttackObject가 CircleCollider를 직접 참조하는 구조로 간다.
 *///////////////////////////////////////////

public class CircleCollider : MonoBehaviour
{
    [SerializeField] private float m_fRadius = 0.5f;
    // 모델 피벗이 실제 판정 중심과 다를 때(예: 몬스터 피벗이 발밑) 로컬 공간 기준으로 보정
    [SerializeField] private Vector3 m_vOffset = Vector3.zero;

    [Header("Debug")]
    [SerializeField] private bool m_bShowDebugGizmo = false;
    [SerializeField] private Color m_tGizmoColor = Color.green;

    public event Action<CircleCollider> OnHitTargetEnter;
    public event Action<CircleCollider> OnHitTargetStay;
    public event Action<CircleCollider> OnHitTargetExit;

    public float Radius => m_fRadius;

    // 회전까지 반영된 실제 판정 중심 (오프셋이 0이면 transform.position과 동일)
    public Vector3 Center => transform.position + transform.rotation * m_vOffset;

    private Vector3 m_vCachedCenter;

    // Center는 쿼터니언 곱셈이 들어있어 쌍마다 반복 조회하면 낭비가 크다(총알 하나가 몬스터
    // 수만큼, 몬스터 하나가 총알 수만큼 매번 다시 계산됨). ColliderManager가 판정 루프 돌기
    // 전에 활성 콜라이더마다 딱 한 번씩 RefreshCenter를 불러 캐시해두고, CheckPair는 이 값만 읽음
    public Vector3 CachedCenter => m_vCachedCenter;

    public void CenterPos()
    {
        m_vCachedCenter = transform.position + transform.rotation * m_vOffset;
        m_vCachedCenter.y = 0.0f;
    }

    // 어떤 레이어끼리 충돌할지는 더 이상 개별 콜라이더가 안 들고, ColliderManager의
    // 레이어 충돌 매트릭스가 중앙에서 결정함 (Unity Physics 설정과 동일한 개념).
    // 여기서는 gameObject.layer를 캐싱만 해서 매 프레임 반복 조회를 피함
    public int Layer { get; private set; }

    // 생애주기 동안 고정되는 자체 ID. ColliderManager가 쌍(pair) 키를 만들 때 이 ID를 사용
    private static int NEXT_ID = 0;

    private bool m_bIsActive = false;
    public bool IsActive => m_bIsActive;

    public int ID { get; private set; }
    private bool m_bActivated = false; // ColliderManager 레이어 리스트에 실제로 등록된 상태인지

    private void Awake()
    {
        ID = NEXT_ID++;
        Layer = gameObject.layer;
    }

    private void Start()
    {
        ColliderManager.m_Instance.RegisterCollider(this);

        // 씬에 미리 배치된 오브젝트는 OnEnable이 ColliderManager.Awake()보다 먼저 돌 수 있어
        // Activate가 씹혔을 수 있음 - Start는 씬의 모든 Awake 이후 호출이 보장되므로 여기서 보정
        if (m_bIsActive && m_bActivated == false)
            Activate();
    }

    private void OnEnable()
    {
        m_bIsActive = true;
        if (ColliderManager.m_Instance != null)
            Activate();
    }

    private void Activate()
    {
        ColliderManager.m_Instance.Activate(this);
        m_bActivated = true;
    }

    private void OnDisable()
    {
        m_bIsActive = false;
        if (m_bActivated)
        {
            ColliderManager.m_Instance.UnActivate(this);
            m_bActivated = false;
        }
    }

    // ColliderManager가 쌍(pair) 상태를 판정한 뒤 Enter/Stay/Exit에 맞춰 호출
    public void OnEnterCollider(CircleCollider _refOther)
    {
        OnHitTargetEnter?.Invoke(_refOther);
    }

    public void OnStayCollider(CircleCollider _refOther)
    {
        OnHitTargetStay?.Invoke(_refOther);
    }

    public void OnExitCollider(CircleCollider _refOther)
    {
        OnHitTargetExit?.Invoke(_refOther);
    }

    private void OnDrawGizmos()
    {
        if (m_bShowDebugGizmo == false)
            return;

        Gizmos.color = m_tGizmoColor;
        Gizmos.DrawWireSphere(Center, m_fRadius);
    }
}
