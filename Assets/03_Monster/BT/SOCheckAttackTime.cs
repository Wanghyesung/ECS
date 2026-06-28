using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
             SOCheckAttackTimeNode
기능 : 현재 몬스터 공격 가능한 기술이 있는지 체크
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_CheckAttackTimeNode", menuName = "Game/Monster/ActionNode/CheckAttackTimeNode")]
public class SOCheckAttackTimeNode : SONode
{
    [SerializeField] private int m_iAttackIdx = -1;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (m_iAttackIdx == -1)
            return eNodeState.Failure;

        var listTime = _refBB.ListCurAttackTime;
        if (listTime == null || _refBB.CurrentAttackIdx == -1)
            return eNodeState.Failure;


        if (_refBB.CurrentAttackTime > listTime[m_iAttackIdx])
        {
            _refBB.CurrentAttackIdx = m_iAttackIdx;

            var listAttackObj = _refBB.Owner.ListAttackObject[m_iAttackIdx];
            float fSpawnTime = listAttackObj.SpawnObjectInfo.AttackInfo.Cooldown;

            listTime[m_iAttackIdx] = Time.time + fSpawnTime;
            _refBB.ListCurAttackObject.Clear();
            return eNodeState.Success;
        }


        return eNodeState.Failure;
    }
}
