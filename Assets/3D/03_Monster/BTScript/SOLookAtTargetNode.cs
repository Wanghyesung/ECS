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

    //[SerializeField] private float m_fMaxAimTime = 1.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null)
        {
            _refBB.AimTimer = 0.0f;
            return eNodeState.Failure;
        }

        // Offset은 조준 방향을 재는 축(위치 기준점)일 뿐, 실제로 회전시키는 대상은 항상 Owner(보스 본체) 하나뿐이다.
        // Offset은 Owner의 자식이라, 여기까지 같이 회전시키면 같은 프레임에 부모/자식이 각자 월드 회전을
        // 따로 맞추려다 서로를 밀어내며 떨리는 현상이 생긴다
        Transform refAxisTr = _refBB.OwnerOffset != null ? _refBB.OwnerOffset : _refBB.Owner.transform;
        Transform refBodyTr = _refBB.Owner.transform;

        Vector3 vDir = _refBB.TargetTr.position - refAxisTr.position;
        if (vDir.sqrMagnitude < 0.0001f)
        {
            _refBB.AimTimer = 0.0f;
            return eNodeState.Success;
        }

        Quaternion qTargetRot = Quaternion.LookRotation(vDir.normalized);

        // 임계 안이면 목표 회전으로 딱 맞춰놓고 종료 - 미세 오차가 남아 다시 Running으로 되돌아가는 것을 막음
        if (Quaternion.Angle(refBodyTr.rotation, qTargetRot) < m_fAngleThreshold)
        {
            refBodyTr.rotation = qTargetRot;
            _refBB.AimTimer = 0.0f;
            return eNodeState.Success;
        }

        refBodyTr.rotation = Quaternion.RotateTowards(refBodyTr.rotation, qTargetRot, m_fRotateSpeed * Time.deltaTime);

        return eNodeState.Running;
    }
}
