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

    // 타겟이 계속 움직이면(플레이어 이동 + BobMove의 상하 부유) 따라가는 데 필요한 각속도가
    // m_fRotateSpeed 상한을 넘어, 잔여 오차가 m_fAngleThreshold 아래로 영원히 안 떨어질 수 있다.
    // 그러면 이 노드가 계속 Running을 반환하고 Sequence/Select가 인덱스를 latch해
    // 뒤의 발사 노드까지 도달하지 못한다. 이 시간을 넘기면 정렬이 덜 됐어도 Success로 빠져나간다
    [SerializeField] private float m_fMaxAimTime = 1.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null)
        {
            _refBB.AimTimer = 0.0f;
            return eNodeState.Failure;
        }

        // 각도를 재는 트랜스폼과 회전을 적용하는 트랜스폼은 반드시 같아야 한다.
        // 이전에는 OwnerOffset(보스는 전방 108유닛 지점)을 기준으로 방향을 구하면서 회전은 Owner에
        // 적용해, 회전할수록 기준점이 휘둘려 목표 각도가 계속 바뀌는 피드백 루프가 생겼다.
        // 조준은 몬스터 본체를 돌리는 것이므로 기준을 Owner로 통일한다(발사 위치는 Weapon의 FireTr가 담당)
        Transform refOwnerTr = _refBB.Owner.transform;

        Vector3 vDir = _refBB.TargetTr.position - refOwnerTr.position;
        if (vDir.sqrMagnitude < 0.0001f)
        {
            _refBB.AimTimer = 0.0f;
            return eNodeState.Success;
        }

        Quaternion qTargetRot = Quaternion.LookRotation(vDir.normalized);

        // 임계 안이면 목표 회전으로 딱 맞춰놓고 종료 - 미세 오차가 남아 다시 Running으로 되돌아가는 것을 막음
        if (Quaternion.Angle(refOwnerTr.rotation, qTargetRot) < m_fAngleThreshold)
        {
            refOwnerTr.rotation = qTargetRot;
            _refBB.AimTimer = 0.0f;
            return eNodeState.Success;
        }

        refOwnerTr.rotation = Quaternion.RotateTowards(
            refOwnerTr.rotation, qTargetRot, m_fRotateSpeed * Time.deltaTime);

        _refBB.AimTimer += Time.deltaTime;
        if (m_fMaxAimTime > 0.0f && _refBB.AimTimer >= m_fMaxAimTime)
        {
            _refBB.AimTimer = 0.0f;
            return eNodeState.Success;
        }

        return eNodeState.Running;
    }
}
