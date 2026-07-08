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
    //결과가 해당 Same값과 동일해야지 통과
    [SerializeField] private bool m_bSame = true;
    [SerializeField] private eEntityState m_eCheckState = eEntityState.Idle;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        eEntityState eState = _refBB.ObjInfo.State;
        bool bResult = eState == m_eCheckState;

        if(m_bSame == bResult)
            return eNodeState.Success;

        return eNodeState.Failure;
    }
}
