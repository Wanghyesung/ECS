using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "SO_CheckState", menuName = "Game/Monster/ActionNode/CheckStateNode")]

/*///////////////////////////////////////////
                SOCheckStateNode
기능 : 몬스터가 어떤 상태인지 체크
 *///////////////////////////////////////////

public class SOCheckStateNode : SONode
{
    [SerializeField] private eEntityState m_eCheckState = eEntityState.Idle;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        eEntityState eState = _refBB.ObjInfo.State;
        if (eState == m_eCheckState)
            return eNodeState.Success;

        return eNodeState.Failure;
    }
}
