using UnityEngine;

[CreateAssetMenu(fileName = "SO_Feature_UnlockMissile", menuName = "Game/Feature/Unlock Missile")]
public class SOFeatureUnlockMissile : SOFeature
{
    [Header("Effect")]
    [SerializeField] private Weapon.eWeaponType m_eTargetWeaponType = Weapon.eWeaponType.Missile;

    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.UnlockWeapon(m_eTargetWeaponType);
    }
}
