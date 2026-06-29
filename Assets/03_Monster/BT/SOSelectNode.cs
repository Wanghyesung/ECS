using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*///////////////////////////////////////////
                SelectNode
��� : �ڽ� ��� �߿��� ������ �� �ִ� ��带 ã�� ���
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_SelectNode", menuName = "Game/Monster/SelectNode")]

public class SOSelectNode : SOListNode
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

            if (eState == eNodeState.Success)
            {
                iCurrentIdx = 0;
                return eNodeState.Success;
            }


            //���� �õ����̶�� ���� ���� ���
            else if (eState == eNodeState.Running)
            {
                iCurrentIdx = i;
                return eNodeState.Running;
            }
        }

        iCurrentIdx = 0;
        return eNodeState.Failure;
    }
}

