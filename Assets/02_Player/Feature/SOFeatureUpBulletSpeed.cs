using UnityEngine;

/*///////////////////////////////////////////
            SOFeatureUpBulletSpeed
기능 : 플레이어 무기의 총알 속도를 높여주는 기능
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_FeatureUpBulletSpeed", menuName = "Game/Feature/FeatureUpBulletSpeed")]
public class SOFeatureUpBulletSpeed : SOFeature
{
    [SerializeField] private float m_fSpeedIncrease = 2.0f;
    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.UpBulletSpeed(m_fSpeedIncrease);
    }
}
