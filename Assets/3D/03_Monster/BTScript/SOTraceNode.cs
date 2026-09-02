using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*///////////////////////////////////////////
            SOTraceNode
기능 : 몬스터의 이동속드를 기준으로 플레이어를 따라가는 기능
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_TraceNode", menuName = "Game/Monster/ActionNode/TraceNode")]
public class SOTraceNode : SONode
{
    [SerializeField] private float m_fTargetDistance = 200f;   // 목적지까지 갈 거리

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null)
            return eNodeState.Failure;

        Transform refOwnerTr = _refBB.Owner.transform;
        Vector3 vDir = _refBB.TargetTr.position - refOwnerTr.position;

        if (vDir.sqrMagnitude > m_fTargetDistance)
            return eNodeState.Success;

        Vector3 vStep = _refBB.ObjInfo.Speed * vDir.normalized * Time.deltaTime;
        _refBB.Owner.transform.position += vStep;
        return eNodeState.Running;
    }
}
