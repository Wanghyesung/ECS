using UnityEngine;

/*///////////////////////////////////////////
            SOFeatureUnlockWeapon
기능 : 플레이어의 HP를 회복시키는 기능
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_FeatureUPHP", menuName = "Game/Feature/SOFeatureUPHP")]
public class SOFeatureUPHP : SOFeature
{
    [SerializeField] private float m_fUpHpRatio;
    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.UpHPRatio(_iNewLevel);
    }

    public override void Cancel(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.DownHPRatio(_iNewLevel);

    }
}
