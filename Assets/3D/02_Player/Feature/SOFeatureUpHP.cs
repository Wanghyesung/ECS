using UnityEngine;

/*///////////////////////////////////////////
            SOFeatureUnlockWeapon
기능 : 플레이어의 HP를 회복시키는 기능
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_FeatureUPHP", menuName = "Game/Feature/SOFeatureUPHP")]
public class SOFeatureUPHP : SOFeature
{
    [Tooltip("한 번 고를 때마다 회복하는 양. 플레이어 MaxHP에 대한 비율")]
    [Range(0.0f, 1.0f)]
    [SerializeField] private float m_fUpHpRatio;

    // 회복량은 획득 횟수가 아니라 이 SO의 비율로 결정된다 (_iNewLevel을 넘기면 2번째 획득부터 MaxHP 2배를 회복)
    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.UpHPRatio(m_fUpHpRatio);
    }

    public override void Cancel(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.DownHPRatio(m_fUpHpRatio * _iNewLevel);

    }
}
