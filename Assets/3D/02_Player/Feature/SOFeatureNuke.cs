using UnityEngine;

/*///////////////////////////////////////////
                SOFeatureNuke
기능 : 확정되는 순간 즉시 발동하는 레전더리 카드. 미사일/데미지는 다른 무기와 같은 SOAttackInfo로
       정의하고(PoolPrefab = NukeMissile, Damage = 최대), 씬의 NukeStrike(맵 중앙 앵커)에 발사를 위임.
       등급·가중치·확률은 이 SO의 Tier/Weight + SOJokerCard 등급 곡선이 담당
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_FeatureNuke", menuName = "Game/Feature/SOFeatureNuke")]
public class SOFeatureNuke : SOFeature
{
    [SerializeField] private SOAttackInfo m_SOAttackInfo;

    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        if (NukeStrike.Current == null)
        {
            Debug.Log("NukeStrike가 씬에 없어 핵폭탄 카드가 발동되지 않음 - BattleScene에 NukeCenter 배치 필요");
            return;
        }

        NukeStrike.Current.Fire(m_SOAttackInfo);
    }

    // 조커 실패로 몰수되어도 이미 터진 뒤라 되돌릴 게 없음
    public override void Cancel(Player _refPlayer, int _iNewLevel) { }
}
