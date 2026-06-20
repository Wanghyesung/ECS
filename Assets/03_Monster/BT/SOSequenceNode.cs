using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                Sequence
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_SequenceNode", menuName = "Game/Monster/SequenceNode")]
public class SOSequenceNode : SOListNode
{

    private int iCurrentIdx = 0;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        for (int i = iCurrentIdx; i < listNode.Count; ++i)
        {
            eNodeState eState = listNode[i].Execute(_refBB);

            if (eState == eNodeState.Failure)
            {
                iCurrentIdx = 0;
                return eNodeState.Failure;
            }

            else if (eState == eNodeState.Running)
            {
                iCurrentIdx = i;
                return eNodeState.Running;
            }
        }

        return eNodeState.Success;
    }

}
