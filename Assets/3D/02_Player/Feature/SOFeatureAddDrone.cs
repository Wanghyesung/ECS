using UnityEngine;

[CreateAssetMenu(fileName = "SO_FeatureAddDrone", menuName = "Game/Feature/FeatureAddDrone")]
/*///////////////////////////////////////////
            SOFeatureAddDrone
기능 : 플레이어가 드론을 생성할 수 있는 기능
 *///////////////////////////////////////////
public class SOFeatureAddDrone : SOFeature
{
    public override void Apply(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.ActivDrone();
    }

    public override void Cancel(Player _refPlayer, int _iNewLevel)
    {
        _refPlayer.UnActivDrone();
    }
}
