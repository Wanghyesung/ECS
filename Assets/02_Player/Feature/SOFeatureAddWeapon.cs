using UnityEngine;

[CreateAssetMenu(fileName = "SO_FeatureAddWeapon", menuName = "Game/Feature/SOFeatureAddWeapon")]
/*///////////////////////////////////////////
            SOFeatureUnlockWeapon
기능 : 플레이어가 지정된 무기를 추가할 수 있게 하는 기능
 *///////////////////////////////////////////
public class SOFeatureAddWeapon : SOFeature
{
    [Header("Effect")]
    [SerializeField] private Weapon.eWeaponType m_eTargetWeaponType = Weapon.eWeaponType.Missile;

    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.SetActiveWweapon(m_eTargetWeaponType);
    }
}
