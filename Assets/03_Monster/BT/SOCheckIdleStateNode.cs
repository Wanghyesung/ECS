using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "SO_CheckIdleState", menuName = "Game/Monster/ActionNode/CheckIdleState")]

/*///////////////////////////////////////////
                CheckIdleState
기능 : 몬스터가 Idle상태인지 체크
 *///////////////////////////////////////////

public class SOCheckIdleStateNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        eEntityState eState = _refBB.ObjInfo.State;

        if (eState == eEntityState.Idle)
            return eNodeState.Success;

        return eNodeState.Failure;
    }
}
