using UnityEngine;

/*///////////////////////////////////////////
            SOFeatureUpAttack
기능 : 플레이어의 공격력을 높여주는 기능
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_FeatureUpAttack", menuName = "Game/Feature/FeatureUpAttack")]
public class SOFeatureUpAttack : SOFeature
{
    [Range(0.0f,1.0f)]
    [SerializeField] private float m_fAttackRatio;
    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.UpAttackRatio(_iNewLevel);
    }
}
