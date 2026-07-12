using UnityEngine;

[CreateAssetMenu(fileName = "SO_FeatureUPHP", menuName = "Game/Feature/SOFeatureUPHP")]
public class SOFeatureUPHP : SOFeature
{
    [SerializeField] private float m_fUpHpRatio;
    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.UpHPRatio(m_fUpHpRatio);
    }
}
