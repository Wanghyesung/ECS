using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_TraceNode", menuName = "Game/Monster/ActionNode/TraceNode")]

/*///////////////////////////////////////////
                TraceNode
기능 : Min<= Value <= Max 범위 내 플레이어 탐색, Min범위 안으로 들어오면 추격 종료
 *///////////////////////////////////////////
public class SOTraceNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null || _refBB.m_refAgent == null || _refBB.Owner == null)
            return eNodeState.Failure;

        Vector3 vOwnerPos = _refBB.Owner.transform.position;
        float fDistance = Vector3.Distance(vOwnerPos, _refBB.TargetTr.position);

        
        if (fDistance > _refBB.TraceMaxDistance)
            return eNodeState.Failure;

        if (fDistance <= _refBB.TraceMinDistance)
        {
            if (_refBB.m_refAgent.hasPath)
                _refBB.m_refAgent.ResetPath();

            return eNodeState.Success;
        }

        _refBB.m_refAgent.SetDestination(_refBB.TargetTr.position);
        return eNodeState.Running;
    }
}
