using UnityEngine;

/*///////////////////////////////////////////
            SOLookAtLoopNode
기능 : 일정 속도로 타겟 방향을 바라보는 노드       
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_LookAtNode", menuName = "Game/Monster/ActionNode/LookAtNode")]
public class SOLookAtNode : SONode
{
    [SerializeField] private float m_fRotateSpeed = 90f;   // 초당 회전 각도 (degree/s)

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null)
            return eNodeState.Failure;

        Transform refOwnerTr = _refBB.Owner.transform;
        Vector3 vDir = _refBB.TargetTr.position - refOwnerTr.position;

        Quaternion qTargetRot = Quaternion.LookRotation(vDir.normalized);
        refOwnerTr.rotation = Quaternion.RotateTowards(refOwnerTr.rotation, qTargetRot, m_fRotateSpeed * Time.deltaTime);

        return eNodeState.Success;
    }
}
