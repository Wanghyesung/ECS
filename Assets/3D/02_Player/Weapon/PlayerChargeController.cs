using R3;
using UnityEngine;

/*///////////////////////////////////////////
                PlayerChargeController
목적 : 플레이어의 누름·놓음·취소 입력을 무기의 WeaponCon에 전달한다.
 *///////////////////////////////////////////
public sealed class PlayerChargeController : MonoBehaviour
{
    [SerializeField] private Weapon m_refChargeWeapon;
    [SerializeField] private Aim m_refAim;
    [SerializeField] private TargetScanner m_refTargetScanner;

    private WeaponCon m_refWeaponCon;
    private Player m_refPlayer;

    public void Configure(Weapon _refWeapon, Aim _refAim, TargetScanner _refTargetScanner)
    {
        m_refChargeWeapon = _refWeapon;
        m_refAim = _refAim;
        m_refTargetScanner = _refTargetScanner;
        m_refWeaponCon = m_refChargeWeapon != null
            ? m_refChargeWeapon.GetComponent<WeaponCon>()
            : null;
    }

    private void Awake()
    {
        m_refPlayer = GetComponent<Player>();
        if (m_refChargeWeapon != null)
            m_refWeaponCon = m_refChargeWeapon.GetComponent<WeaponCon>();
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
        if (!isActiveAndEnabled || m_refWeaponCon == null)
            return false;
        if (m_refPlayer == null || m_refAim == null)
            return false;
        return Time.timeScale > 0f && m_refPlayer.ObjectInfo.State != eEntityState.Dead;
    }

    private void StartCharge()
    {
        if (CanCharge())
            m_refWeaponCon.Begin();
    }

    private void ReleaseCharge()
    {
        if (!CanCharge())
        {
            CancelCharge();
            return;
        }
        Transform refTarget = m_refTargetScanner != null ? m_refTargetScanner.Target : null;
        m_refWeaponCon.Release(m_refAim.TargetPosition, refTarget);
    }

    private void CancelCharge()
    {
        if (m_refWeaponCon != null)
            m_refWeaponCon.Cancel();
    }
}
