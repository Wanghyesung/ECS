using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
            SOBobMoveNode
기능 : 몬스터의 자연스러운 움직임을 위해서 위아래로 자연스러운 움직임 연출
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_BobMoveNode", menuName = "Game/Monster/ActionNode/BobMoveNode")]
public class SOBobMoveNode : SONode
{
    [SerializeField] private float m_fBobSpeed = 0.0f;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        float yOffset = Mathf.Sin(Time.time) * m_fBobSpeed;

        Transform refOwnerTr = _refBB.Owner.transform;
        refOwnerTr.position += refOwnerTr.up * yOffset * Time.deltaTime;

        return eNodeState.Success;
    }
}

