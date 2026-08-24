using System;
using UnityEngine;

/*///////////////////////////////////////////
                BaseCollider
목적 : PhysX Collider 없이 ColliderManager가 판정하는 콜라이더들의 공통 베이스.
       ID/레이어 캐싱, Activate/UnActivate 생애주기, Enter/Stay/Exit 이벤트를 여기서
       한 번만 구현하고 CircleCollider/ObbCollider가 상속받는다. 도형별로 다른 부분
       (반지름/half-extent, 중심 갱신 방식)만 서브클래스가 채운다.

       eColliderShape도 같은 파일에 co-locate — 이 프로젝트에는 이미 Bullet.cs가
       IAttackObject 인터페이스를 같은 파일에 두는 선례가 있어, 작고 강하게 결합된
       보조 타입을 본체 클래스와 한 파일에 두는 게 낯선 패턴이 아님.
 *///////////////////////////////////////////

public enum eColliderShape
{
    Circle,
    Box,
}

public abstract class BaseCollider : MonoBehaviour
{
    public event Action<BaseCollider> OnHitTargetEnter;
    public event Action<BaseCollider> OnHitTargetStay;
    public event Action<BaseCollider> OnHitTargetExit;

    // 생애주기 동안 고정되는 자체 ID. ColliderManager가 쌍(pair) 키를 만들 때 이 ID를 사용
    private static int NEXT_ID = 0;

    public int Layer { get; private set; }
    public int ID { get; private set; }
    public eColliderShape Shape { get; protected set; }

    // 판정에 쓰이는 실제 중심 좌표. Static이 아니면 매 프레임 ColliderManager의
    // RefreshCenterJob이 병렬로 갱신한 뒤 ApplyCachedCenter로 되돌려 쓴다(§ApplyCachedCenter 참고)
    public Vector3 CachedCenter { get; protected set; }

    private bool m_bActivated = false; // ColliderManager 레이어 리스트에 실제로 등록된 상태인지

    public bool StaticObject { get; protected set; }

    // 도형 무관 공통 반지름 상한 - 그리드 셀 크기 산정과 구-구 선판정(broad bound check)에
    // 쓰인다. Circle은 자기 Radius, Box는 half-extent 대각선 길이(Start에서 한 번만 계산)
    public virtual float BoundingRadius => 0f;

    // 모델 피벗이 실제 판정 중심과 다를 때 로컬 공간 기준으로 보정하는 값 - Circle/Obb 둘 다
    // 자기 m_vOffset을 반환하도록 override. ColliderManager가 TransformAccessArray 등록
    // 시점에 한 번 읽어가는 용도(Burst Job은 관리형 필드를 직접 못 읽으므로 값으로 넘겨야 함)
    public virtual Vector3 Offset => Vector3.zero;

    protected virtual void Awake()
    {
        ID = NEXT_ID++;
        Layer = gameObject.layer;

        StaticObject = gameObject.isStatic;
    }

    // virtual 필수: ObbCollider처럼 서브클래스가 축/크기 계산용 Start()를 따로 둘 때,
    // private로 두면 서브클래스의 Start()가 이걸 오버라이드가 아니라 "가려버려서"(hiding)
    // 이 등록 로직 자체가 영원히 안 불리는 사고가 남 - 실제로 겪은 버그라 반드시 virtual+override
    protected virtual void Start()
    {
        ColliderManager.m_Instance.RegisterCollider(this);

        // 씬에 미리 배치된 오브젝트는 OnEnable이 ColliderManager.Awake()보다 먼저 돌 수 있어
        // Activate가 씹혔을 수 있음 - Start는 씬의 모든 Awake 이후 호출이 보장되므로 여기서 보정
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

    // 생애주기 중 최초 1회(Start)용 동기 계산 경로 - Circle/Obb 둘 다 이걸 오버라이드해서
    // 실제로 CachedCenter(Obb는 축도 함께)를 갱신함. 매 프레임 갱신은 더 이상 이 경로를 안 쓰고
    // ColliderManager가 TransformAccessArray + Job(RefreshCenterJob)으로 병렬 처리한 뒤
    // ApplyCachedCenter로 되돌려 쓴다(§ColliderManager.PreLoadCenter 참고) - 기본 구현은
    // 빈 채로 둬서, 다른 도형이 추가될 때 굳이 오버라이드 안 해도 안전하게 no-op
    public virtual void RefreshCenter() { }

    // ColliderManager가 RefreshCenterJob 결과를 되돌려 쓸 때만 호출 - CachedCenter의
    // protected set을 이 안에서만 대신 열어준다
    public void ApplyCachedCenter(Vector3 _vCenter) => CachedCenter = _vCenter;

    // ColliderManager가 쌍(pair) 상태를 판정한 뒤 Enter/Stay/Exit에 맞춰 호출
    public void OnEnterCollider(BaseCollider _refOther) => OnHitTargetEnter?.Invoke(_refOther);
    public void OnStayCollider(BaseCollider _refOther) => OnHitTargetStay?.Invoke(_refOther);
    public void OnExitCollider(BaseCollider _refOther) => OnHitTargetExit?.Invoke(_refOther);
}
