using UnityEngine;

/*///////////////////////////////////////////
            SOOrbitNode
기능 : 타겟(플레이어) 주변을 XZ 평면상에서 공전
       현재 XZ 거리를 반경으로 그대로 사용 → 진입 즉시 공전 시작
       Y축을 건드리지 않아 SOBobMoveNode와 독립적으로 동작
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_OrbitNode", menuName = "Game/Monster/ActionNode/OrbitNode")]
public class SOOrbitNode : SONode
{
    [SerializeField] private float m_fOrbitSpeed = 60f;   // 도/초 (음수 = 반대 방향)

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.ObjInfo.State != eEntityState.Idle)
            return eNodeState.Failure;

        if (_refBB.TargetTr == null)
            return eNodeState.Failure;
        
        Transform refOwnerTr = _refBB.Owner.transform;
        Vector3 vTargetPos   = _refBB.TargetTr.position;

        // 현재 XZ 방향 역산 (상태 저장 불필요)
        Vector3 vToOwner = refOwnerTr.position - vTargetPos;
        vToOwner.y = 0f;

        if (vToOwner.sqrMagnitude < 0.0001f)
            vToOwner = Vector3.right;

        // 현재 XZ 거리를 반경으로 유지하며 회전
        float fRadius = vToOwner.magnitude;
        Vector3 vOrbitDir = Quaternion.Euler(0f, m_fOrbitSpeed * Time.deltaTime, 0f) * vToOwner.normalized;

        Vector3 vNewPos = vTargetPos + vOrbitDir * fRadius;
        vNewPos.y = refOwnerTr.position.y;  // Y는 유지 (SOBobMoveNode가 독립 제어)

        refOwnerTr.position = vNewPos;

        return eNodeState.Success;
    }
}
