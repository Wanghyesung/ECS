using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum eNodeState
{
    Success,
    Failure,
    Running,
}


public abstract class Node : ScriptableObject
{
    public abstract eNodeState Execute(BlackBoard _refBB);

}

/*///////////////////////////////////////////
                ListNode
 *///////////////////////////////////////////
public abstract class ListNode : ScriptableObject
{
    protected List<Node> listNode = new List<Node>();

    public abstract eNodeState Execute(BlackBoard _refBB);
}


public class SelectNode : ListNode
{
    private int iCurrentIdx = 0;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        for(int i = iCurrentIdx; i < listNode.Count; ++i)
        {
            eNodeState eState = listNode[iCurrentIdx].Execute(_refBB);

            if (eState == eNodeState.Success)
            {
                iCurrentIdx = 0;
                return eNodeState.Success;
            }


            //만약 시도중이라면 현제 구간 기억
            else if(eState == eNodeState.Running) 
            {
                iCurrentIdx = i;
                return eNodeState.Running;
            }
        }

        return eNodeState.Failure;
    }
}


public class Sequence : ListNode
{

    private int iCurrentIdx = 0;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        for (int i = iCurrentIdx; i < listNode.Count; ++i)
        {
            eNodeState eState = listNode[iCurrentIdx].Execute(_refBB);

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


/*///////////////////////////////////////////
              BehaviorTree
 *///////////////////////////////////////////

public class BehaviorTree : MonoBehaviour
{
    [SerializeField] private BlackBoard m_refBB = new BlackBoard();
    

    private void Awake()
    {
                

    }


}
