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

        // 조준은 몬스터 본체를 돌리는 것이므로 기준을 Owner로 통일한다(발사 위치는 Weapon의 FireTr가 담당)
        Transform refOwnerTr = _refBB.Owner.transform;

        Vector3 vDir = _refBB.TargetTr.position - refOwnerTr.position;
        if (vDir.sqrMagnitude < 0.0001f)
        {
            _refBB.AimTimer = 0.0f;
            return eNodeState.Success;
        }

        Quaternion qTargetRot = CalcAimRotation(_refBB, vDir);

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

    //Offset을 지정하면 해당 지정된 Tr을 기준으로 모델을 회전한다 
    private static Quaternion CalcAimRotation(BlackBoard _refBB, Vector3 _vToTarget)
    {
        Quaternion qLook = Quaternion.LookRotation(_vToTarget);
        if (_refBB.OwnerOffset == null)
            return qLook;

        /*
         * 월드 기준으로 구해진 방향(계속 변하는 값)에 캐릭터의 역회전을 곱해버리면, 
         * 캐릭터가 돌았던 만큼을 다시 거꾸로 되돌려버리는 효과가 납니다.
         */
        Transform refOwnerTr = _refBB.Owner.transform;
        Vector3 vLocalOffset = Quaternion.Inverse(refOwnerTr.rotation) * (_refBB.OwnerOffset.position - refOwnerTr.position);
        float fPerpSq = vLocalOffset.x * vLocalOffset.x + vLocalOffset.y * vLocalOffset.y;
        float fDistSq = _vToTarget.sqrMagnitude;
        if (fDistSq <= fPerpSq)   // 타겟이 오프셋 반경 안 - 어떤 회전으로도 광선이 못 지나감, 피벗 조준으로 폴백
            return qLook;

        //타고라스 정리(D² = x² + y² + z²)를 역산하여 앞으로 뻗어갈 z값(Mathf.Sqrt(fDistSq - fPerpSq))을 구함
        Vector3 vLocalDir = new Vector3(vLocalOffset.x, vLocalOffset.y, Mathf.Sqrt(fDistSq - fPerpSq));

        // 기본 회전(qLook)에서, 총구가 비껴나가 있는 각도(LookRotation(vLocalDir))만큼을 
        // 반대로 틀어줘서(Inverse) 몸통을 살짝 비틂 -> 총구가 타겟을 정확히 조준하게 됨
        return qLook * Quaternion.Inverse(Quaternion.LookRotation(vLocalDir));
    }
}
