using UnityEngine;

/*///////////////////////////////////////////
            SOLookAtTargetNode
기능 : 일정 속도로 타겟 방향을 바라보는 노드
       바라보는 중 → Running, 완료 → Success
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_LookAtTargetNode", menuName = "Game/Monster/ActionNode/LookAtTargetNode")]
public class SOLookAtTargetNode : SONode
{
    [SerializeField] private float m_fRotateSpeed = 90f;   // 초당 회전 각도 (degree/s)
    [SerializeField] private float m_fAngleThreshold = 2f; // 이 각도 이내면 Success

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null)
            return eNodeState.Failure;

        Transform refOwnerTr = _refBB.Owner.transform;
        Vector3 vDir = _refBB.TargetTr.position - refOwnerTr.position;
        
        if (vDir.sqrMagnitude < 0.0001f)
            return eNodeState.Success;

        Quaternion qTargetRot = Quaternion.LookRotation(vDir.normalized);
        refOwnerTr.rotation = Quaternion.RotateTowards(refOwnerTr.rotation, qTargetRot, m_fRotateSpeed * Time.deltaTime);

        if (Quaternion.Angle(refOwnerTr.rotation, qTargetRot) < m_fAngleThreshold)
            return eNodeState.Success;

        return eNodeState.Running;
    }
}
