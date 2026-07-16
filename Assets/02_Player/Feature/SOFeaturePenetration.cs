using UnityEngine;

/*///////////////////////////////////////////
            SOFeaturePenetration
기능 : 플레이어의 특정 무기가 관통할 수 있게 만드는 기능
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SOFeaturePenetration", menuName = "Game/Feature/SO_FeaturePenetration")]
public class SOFeaturePenetration : SOFeature
{
    [SerializeField] private Weapon.eWeaponType m_eTargetWeaponType = Weapon.eWeaponType.Bullet;

    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.PenetrationWeapon(m_eTargetWeaponType);
    }
}
