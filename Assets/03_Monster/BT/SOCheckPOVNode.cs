using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*///////////////////////////////////////////
                TraceNode
기능 : 몬스터 시야 각도 체크
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_CheckPOVNode", menuName = "Game/Monster/ActionNode/CheckPOVNode")]

public class SOCheckPOVNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        //if (_refBB.TargetTr == null || _refBB.Agent == null || _refBB.Owner == null)
        //    return eNodeState.Failure;
        //
        //Vector3 vOwnerPos = _refBB.Owner.transform.position;
        //Vector3 vDirToTarget = _refBB.TargetTr.position - vOwnerPos;
        //
        //vDirToTarget.y = 0f;
        //float fDistance = vDirToTarget.magnitude;
        //
        //float fHalfPOV = _refBB.POV * 0.5f;
        //float fAngle = Vector3.Angle(_refBB.Owner.transform.forward, vDirToTarget.normalized);
        //if (fAngle > fHalfPOV)
        //    return eNodeState.Failure;

        return eNodeState.Success;
    }
}

