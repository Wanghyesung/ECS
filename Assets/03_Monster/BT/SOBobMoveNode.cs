using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
            SOBobMoveNode
기능 : 몬스터가 부유 효과를 보여줄 수 있게 y축 이동 효과
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_BobMoveNode", menuName = "Game/Monster/ActionNode/BobMoveNode")]
public class SOBobMoveNode : SONode
{
    [SerializeField] private float m_fBobSpeed = 0.0f;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        float fYOffset = Mathf.Sin(Time.time) * m_fBobSpeed;

        Transform refOwnerTr = _refBB.Owner.transform;
        // 로컬 up이 아닌 월드 Y 사용 — LookAt으로 몬스터가 기울어도 상하 부유 유지
        refOwnerTr.position += Vector3.up * fYOffset * Time.deltaTime;

        return eNodeState.Success;
    }
}

