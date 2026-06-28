using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*///////////////////////////////////////////
               SetTargetDirNode
기능 : 총알이 따라가야하는 위치 컴포넌트 전달
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_SetTargetDirNode", menuName = "Game/Monster/ActionNode/SetTargetDirNode")]

public class SOSetTargetDirNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null)
            return eNodeState.Failure;

        var listCurAttackObj = _refBB.ListCurAttackObject;

        foreach(var refBullet in listCurAttackObj)
        {
            if (refBullet.TryGetComponent<GuidedBullet>(out var refGuide))
                refGuide.SetTarget(_refBB.TargetTr);
        }

        return eNodeState.Success;
    }
}
