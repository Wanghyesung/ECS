using UnityEngine;

[CreateAssetMenu(fileName = "SO_Feature_AttackSpeedUp", menuName = "Game/Feature/Attack Speed Up")]
public class SOFeatureAttackSpeedUp : FeatureSO
{
    [Header("Effect")]
    [SerializeField] private Weapon.eWeaponType m_eTargetWeaponType = Weapon.eWeaponType.Bullet;

    [Tooltip("1레벨당 쿨다운 감소율")]
    [SerializeField] [Range(0f, 0.9f)] private float m_fCooldownReduceRate = 0.1f;

    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        float fMultiplier = 1f - (m_fCooldownReduceRate * _iNewLevel);
        _refPlayer.ModifyWeaponCooldown(m_eTargetWeaponType, fMultiplier);
    }
}
