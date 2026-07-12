using UnityEngine;

[CreateAssetMenu(fileName = "SO_FeatureUnlockWeapon", menuName = "Game/Feature/FeatureUnlockWeapon")]
public class SOFeatureUnlockWeapon : SOFeature
{
    [Header("Effect")]
    [SerializeField] private Weapon.eWeaponType m_eTargetWeaponType = Weapon.eWeaponType.Missile;

    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.UnlockWeapon(m_eTargetWeaponType);
    }
}
