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
    public override eNodeState Execute(BlackBoard _refBB)
    {
        var listTime = _refBB.ListCurAttackTime;
        if (listTime == null || _refBB.CurrentAttackIdx == -1)
            return eNodeState.Failure;

        for(int i = 0; i < listTime.Count; ++i)
        {
            if(_refBB.CurrentAttackTime > listTime[i])
            {
                _refBB.CurrentAttackIdx = i;
                
                float fSpawnTime = _refBB.Owner.ListSpawnObject[i].SpawnObjectInfo.AttackInfo.Cooldown;
                listTime[i] = Time.time + fSpawnTime;

                _refBB.ListCurAttackObject.Clear();
                return eNodeState.Success;
            }
        }

        return eNodeState.Failure;
    }
}
