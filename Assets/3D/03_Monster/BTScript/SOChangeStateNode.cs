using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "SO_ChangeStateNode", menuName = "Game/Monster/ActionNode/ChangeStateNode")]

/*///////////////////////////////////////////
              SOChangeStateNode
기능 : 몬스터의 상태를 변경하는 노드
 *///////////////////////////////////////////

public class SOChangeStateNode : SONode
{
    [SerializeField] private eEntityState m_eChangeState = eEntityState.Idle;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        _refBB.Owner.ChangeState(m_eChangeState);
        return eNodeState.Success;
    }
}
