using UnityEngine;

/*///////////////////////////////////////////
            SOFeatureUpDefense
기능 : 플레이어의 방어력을 높여주는 기능
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_FeatureUpDefense", menuName = "Game/Feature/FeatureUpDefense")]
public class SOFeatureUpDefense : SOFeature
{
    [Range(0.0f, 1.0f)]
    [SerializeField] private float m_fDefenseRatio;
    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.UpDefenseRatio(m_fDefenseRatio);
    }

    public override void Cancel(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.DownDefenseRatio(m_fDefenseRatio);

    }
}
