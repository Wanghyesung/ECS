using R3;
using UnityEngine;

// 충돌체의 ID, 활성 수명과 Enter/Stay/Exit 이벤트를 관리한다.

public enum eColliderShape
{
    Circle,
    Box,
}

public abstract class BaseCollider : MonoBehaviour
{
    private readonly Subject<BaseCollider> m_subjectEnter = new();
    private readonly Subject<BaseCollider> m_subjectStay = new();
    private readonly Subject<BaseCollider> m_subjectExit = new();
    public Observable<BaseCollider> OnHitTargetEnter => m_subjectEnter;
    public Observable<BaseCollider> OnHitTargetStay => m_subjectStay;
    public Observable<BaseCollider> OnHitTargetExit => m_subjectExit;

    // 생애주기 동안 고정되는 자체 ID. ColliderManager가 쌍(pair) 키를 만들 때 이 ID를 사용
    private static int NEXT_ID = 0;

    public int Layer { get; private set; }
    public int ID { get; private set; }
    public eColliderShape Shape { get; protected set; }

    // 판정용 중심. 동적 충돌체는 Job 결과로 갱신한다.
    public Vector3 CachedCenter { get; protected set; }

    private bool m_bActivated = false; // ColliderManager 레이어 리스트에 실제로 등록된 상태인지

    public bool StaticObject { get; protected set; }

    // 그리드 탐색과 정밀 판정 전 거리 검사에 사용하는 반지름 상한.
    public virtual float BoundingRadius => 0f;

    // 등록 시 Job에 전달하는 로컬 중심 보정값.
    public virtual Vector3 Offset => Vector3.zero;

    protected virtual void Awake()
    {
        ID = NEXT_ID++;
        Layer = gameObject.layer;

        StaticObject = gameObject.isStatic;
    }

    // 하위 Start는 base.Start()를 호출해야 초기 활성화가 보장된다.
    protected virtual void Start()
    {
        // 매니저 Awake 이전에 생략된 OnEnable 등록을 보완한다.
        if (m_bActivated == false)
            Activate();
    }

    private void OnEnable()
    {
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
        if (m_bActivated)
        {
            ColliderManager.m_Instance.UnActivate(this);
            m_bActivated = false;
        }
    }

    // 초기화와 테스트에서 사용하는 동기 갱신. 매 프레임 갱신은 Job이 담당한다.
    public virtual void RefreshCenter() { }

    // Job 결과를 캐시에 반영한다.
    public void ApplyCachedCenter(Vector3 _vCenter) => CachedCenter = _vCenter;

    // ColliderManager가 쌍(pair) 상태를 판정한 뒤 Enter/Stay/Exit에 맞춰 호출
    public void OnEnterCollider(BaseCollider _refOther) => m_subjectEnter.OnNext(_refOther);
    public void OnStayCollider(BaseCollider _refOther) => m_subjectStay.OnNext(_refOther);
    public void OnExitCollider(BaseCollider _refOther) => m_subjectExit.OnNext(_refOther);
}
