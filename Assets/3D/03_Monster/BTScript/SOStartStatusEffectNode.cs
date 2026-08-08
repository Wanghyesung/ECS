using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
            SOStartStatusEffectNode
기능 : 몬스터가 상태이상을 시작하는 기능
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_StartStatusEffectNode", menuName = "Game/Monster/ActionNode/StartStatusEffectNode")]

public class SOStartStatusEffectNode : SONode
{
    [SerializeField] private eStatusEffect m_eState;
    [SerializeField] private float m_fDuration;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        _refBB.Owner.StartStateEffect(m_eState, m_fDuration);

        return eNodeState.Success;
    }
}

