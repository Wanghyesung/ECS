using R3;
using UnityEngine;

/*///////////////////////////////////////////
                PlayerChargeController
목적 : 좌클릭 누름/뗌 타이밍과 차지율 계산만 담당. 총알 스폰은 전부 기존 Weapon.
       FireCharged()에 위임. 참조한 Weapon은 Player.m_listWeapon에 등록되어
       카드 강화는 받지만, ChargeOnly=true라 자동사격 루프에서는 제외된다
 *///////////////////////////////////////////
public sealed class PlayerChargeController : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField] private Weapon m_refChargeWeapon;   // m_listWeapon에도 등록된, ChargeOnly=true인 그 Weapon

    [Header("Targeting")]
    [SerializeField] private Aim m_refAim;
    [SerializeField] private TargetScanner m_refTargetScanner;

    [Header("Charge Tuning")]
    [SerializeField] private float m_fMaxChargeTime = 2.0f;
    [SerializeField] private float m_fMaxSpeedMultiplier = 2.5f;
    [SerializeField] private float m_fBaseColliderRadius = 0.5f;
    // 주의: BoxColliderGrid 최초 빌드 시점 최대 BoundingRadius*2 를 넘기면 인접 1칸
    // 브로드페이즈가 원거리 대상과의 겹침을 놓칠 수 있음(정적 분할) - 씬의 다른
    // 콜라이더보다 작게 유지
    [SerializeField] private float m_fMaxColliderRadius = 1.2f;
    [SerializeField] private float m_fMaxVisualScale = 2.0f;

    private float m_fChargeTimer;
    private bool m_bCharging;

    // Awake/OnEnable이 아니라 Start - InputManager.m_Instance가 준비된 것이 보장되는 시점
    private void Start()
    {
        InputManager.m_Instance.OnChargeButtonStarted.Subscribe(_ => StartCharge()).AddTo(this);
        InputManager.m_Instance.OnChargeButtonReleased.Subscribe(_ => ReleaseCharge()).AddTo(this);
    }

    private void Update()
    {
        if (m_bCharging == false)
            return;

        m_fChargeTimer = Mathf.Min(m_fChargeTimer + Time.deltaTime, m_fMaxChargeTime);
    }

    private void StartCharge()
    {
        m_bCharging = true;
        m_fChargeTimer = 0.0f;
    }

    private void ReleaseCharge()
    {
        if (m_bCharging == false)
            return;

        m_bCharging = false;
        float fRatio = m_fChargeTimer / m_fMaxChargeTime;   // 2초에서 멈춰있었으므로 자동으로 0~1

        float fSpeedMul = Mathf.Lerp(1.0f, m_fMaxSpeedMultiplier, fRatio);
        float fRadius = Mathf.Lerp(m_fBaseColliderRadius, m_fMaxColliderRadius, fRatio);
        float fScale = Mathf.Lerp(1.0f, m_fMaxVisualScale, fRatio);

        m_refChargeWeapon.FireCharged(m_refAim.TargetPosition, m_refTargetScanner.Target, fSpeedMul, fRadius, fScale);
    }
}
