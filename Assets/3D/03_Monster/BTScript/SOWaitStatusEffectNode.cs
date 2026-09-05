using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/*///////////////////////////////////////////
            WaitStatusEffectNode
기능 : 몬스터가 상태이상이 풀릴때까지 대기
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_WaitStatusEffectNode", menuName = "Game/Monster/ActionNode/WaitStatusEffectNode")]

public class SOWaitStatusEffectNode : SONode
{
    [SerializeField] private eStatusEffect m_eState;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.Owner.CheckStateEffect(m_eState))
            return eNodeState.Success;

        return eNodeState.Running;
    }
}

