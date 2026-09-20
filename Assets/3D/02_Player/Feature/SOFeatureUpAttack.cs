using UnityEngine;

/*///////////////////////////////////////////
            SOFeatureUpAttack
기능 : 플레이어의 공격력을 높여주는 기능
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_FeatureUpAttack", menuName = "Game/Feature/FeatureUpAttack")]
public class SOFeatureUpAttack : SOFeature
{
    [Tooltip("1레벨당 오르는 공격력 증가율. SOObjectInfo.MaxAtack(공격력 증가율 상한 %)에 대한 비율")]
    [Range(0.0f,1.0f)]
    [SerializeField] private float m_fAttackRatio;

    // _iNewLevel은 '몇 번째 획득인가'일 뿐 증가량이 아니다. Repeatable 카드라 한 번 고를 때마다
    // m_fAttackRatio 만큼 누적되므로, 여기서 레벨을 곱하면 이중 가산이 된다
    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.UpAttackRatio(m_fAttackRatio);
    }

    public override void Cancel(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.DownAttackRatio(m_fAttackRatio * _iNewLevel);
    }
}
