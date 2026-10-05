using R3;
using UnityEngine;

/*///////////////////////////////////////////
                PlayerChargeController
목적 : 플레이어의 누름·놓음·취소 입력을 무기의 WeaponCon에 전달한다.
 *///////////////////////////////////////////
[DisallowMultipleComponent]
[RequireComponent(typeof(Player), typeof(Aim), typeof(TargetScanner))]
public sealed class PlayerChargeController : MonoBehaviour
{
    [SerializeField] private WeaponCon m_refWeaponCon;

    private Aim m_refAim;
    private TargetScanner m_refTargetScanner;
    private Player m_refPlayer;

    private void Awake()
    {
        m_refPlayer = GetComponent<Player>();
        m_refAim = GetComponent<Aim>();
        m_refTargetScanner = GetComponent<TargetScanner>();
    }

    private void Start()
    {
        InputManager.m_Instance.OnChargeButtonStarted.Subscribe(_ => StartCharge()).AddTo(this);
        InputManager.m_Instance.OnChargeButtonReleased.Subscribe(_ => ReleaseCharge()).AddTo(this);
        InputManager.m_Instance.OnChargeButtonCanceled.Subscribe(_ => CancelCharge()).AddTo(this);
        Player.OnPlayerDied.Subscribe(_ => CancelCharge()).AddTo(this);
    }

    private void OnDisable() => CancelCharge();

    private bool CanCharge()
    {
        if (isActiveAndEnabled == false)
            return false;
        if (Time.timeScale <= 0f)
            return false;
        return m_refPlayer.ObjectInfo.State != eEntityState.Dead;
    }

    private void StartCharge()
    {
        if (CanCharge() == false)
            return;

        m_refWeaponCon.Begin();
    }

    private void ReleaseCharge()
    {
        if (CanCharge() == false)
        {
            CancelCharge();
            return;
        }
        m_refWeaponCon.Release(m_refAim.TargetPosition, m_refTargetScanner.Target);
    }

    private void CancelCharge() => m_refWeaponCon.Cancel();
}
