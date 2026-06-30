using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                Sequence
기능 : 지정된 노드를 순차적으로 실행
       지정된 노드들이 하나의 행동으로 모두 성공해야지 성공
       하나라도 실패하면 실패로 처리
        
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_SequenceNode", menuName = "Game/Monster/SequenceNode")]
public class SOSequenceNode : SOListNode
{
    private int iCurrentIdx = 0;

    private void OnEnable()
    {
        iCurrentIdx = 0;
    }

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

        iCurrentIdx = 0;
        return eNodeState.Success;
    }

}
