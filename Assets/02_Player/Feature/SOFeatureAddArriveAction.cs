using UnityEngine;

/*///////////////////////////////////////////
              SOFeatureAddAction
기능 : 지정된 ActionSO를 플레이어 무기에 적용하는 기능
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_FeatureAddArriveAction", menuName = "Game/Feature/FeatureAddArriveAction")]
public class SOFeatureAddArriveAction : SOFeature
{
    [SerializeField] private Weapon.eWeaponType m_eTargetWeaponType = Weapon.eWeaponType.Bullet;
    [SerializeField] private SOBulletAction m_SOBulletAction = null;

    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.AddWeaponArriveAction(m_eTargetWeaponType, m_SOBulletAction);
    }
}
