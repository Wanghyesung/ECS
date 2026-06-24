using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
             SOTryAttackNode
±â´É : 
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_TryAttackNode", menuName = "Game/Monster/TryAttackNode")]
public class SOTryAttackNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        var listSpawn = _refBB.ListCurAttackTime;
        for(int i = 0; i<listSpawn.Count; ++i)
        {
            if(Time.time > listSpawn[i])
            {
                _refBB.CurrentAttackIdx = i;
                return eNodeState.Success;
            }
        }

        return eNodeState.Failure;
    }
}
