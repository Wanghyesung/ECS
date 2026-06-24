using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*///////////////////////////////////////////
                SelectNode
기능 : 자식 노드 중에서 성공할 수 있는 노드를 찾는 기능
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_SelectNode", menuName = "Game/Monster/SelectNode")]

public class SOSelectNode : SOListNode
{
    private int iCurrentIdx = 0;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        for (int i = iCurrentIdx; i < listNode.Count; ++i)
        {
            eNodeState eState = listNode[i].Execute(_refBB);

            if (eState == eNodeState.Success)
            {
                iCurrentIdx = 0;
                return eNodeState.Success;
            }


            //만약 시도중이라면 현제 구간 기억
            else if (eState == eNodeState.Running)
            {
                iCurrentIdx = i;
                return eNodeState.Running;
            }
        }

        return eNodeState.Failure;
    }
}

