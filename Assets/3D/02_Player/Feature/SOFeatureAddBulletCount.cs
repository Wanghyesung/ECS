using UnityEngine;

/*///////////////////////////////////////////
            SOFeatureAddBulletCount
기능 : 플레이어 특정 타입 무기의 발당 탄수를 늘리는 기능 (멀티샷)
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_FeatAddBulletCount", menuName = "Game/Feature/SOFeatureAddBulletCount")]
public class SOFeatureAddBulletCount : SOFeature
{
    [Header("Effect")]
    [SerializeField] private Weapon.eWeaponType m_eTargetWeaponType = Weapon.eWeaponType.Bullet;

    [Tooltip("1레벨당 늘어나는 발당 탄수")]
    [SerializeField] private int m_iCountPerLevel = 1;

    // _iNewLevel은 '몇 번째 획득인가'일 뿐 - Repeatable 카드라 고를 때마다 한 단계씩 누적 (SOFeatureUpAttack과 같은 이유)
    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.AddWeaponBulletCount(m_eTargetWeaponType, m_iCountPerLevel);
    }

    // CancelFeature가 레벨을 0으로 되돌리므로 누적분 전부를 뺀다
    public override void Cancel(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.AddWeaponBulletCount(m_eTargetWeaponType, -m_iCountPerLevel * _iNewLevel);
    }
}
