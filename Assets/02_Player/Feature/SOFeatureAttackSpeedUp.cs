using UnityEngine;

[CreateAssetMenu(fileName = "SO_Feature_AttackSpeedUp", menuName = "Game/Feature/Attack Speed Up")]


/*///////////////////////////////////////////
            SOFeatureAttackSpeedUp
기능 : 플레이어 공격하는 무기의 쿨타임을 줄이는 기능
 *///////////////////////////////////////////
public class SOFeatureAttackSpeedUp : SOFeature
{
    [Header("Effect")]
    [SerializeField] private Weapon.eWeaponType m_eTargetWeaponType = Weapon.eWeaponType.Bullet;

    [Tooltip("1레벨당 쿨다운 감소율")]
    [SerializeField] [Range(0f, 0.9f)] private float m_fCooldownReduceRate = 0.03f;

    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        float fMultiplier = 1f - (m_fCooldownReduceRate * _iNewLevel);
        _refPlayer.SetWeaponCooldown(m_eTargetWeaponType, fMultiplier);
    }

    public override void Cancel(Player _refPlayer, int _iNewLevel)
    {
        float fMultiplier = 1f + (m_fCooldownReduceRate * _iNewLevel);
        _refPlayer.SetWeaponCooldown(m_eTargetWeaponType, fMultiplier);
    }
}
