using UnityEngine;

/*///////////////////////////////////////////
            SOFeatureUpSpeed
기능 : 플레이어의 이동속도를 높여주는 기능
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_FeatureUpSpeed", menuName = "Game/Feature/FeatureUpSpeed")]
public class SOFeatureUpSpeed : SOFeature
{
    [Range(0.0f, 1.0f)]
    [SerializeField] private float m_fSpeedRatio;
    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.UpSpeedRatio(m_fSpeedRatio);
    }
}
