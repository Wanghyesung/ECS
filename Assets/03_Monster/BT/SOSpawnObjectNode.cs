using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/*///////////////////////////////////////////
             SpawnObjectNode
기능 : 몬스터가 공격 오브젝트를 소환하는 기능
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_SpawnObjectNode", menuName = "Game/Monster/ActionNode/SpawnObjectNode")]
public class SOSpawnObjectNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        SpawnInfo refSpawnInfo = _refBB.CurrentAttackSpawn;
        if (refSpawnInfo == null || _refBB.TargetTr == null)
            return eNodeState.Failure;


        int iPoolCount = ObjectPool.m_Instance.GetObjectCount(refSpawnInfo.Weapon.FireBulletType);
        int iSpawnCount = refSpawnInfo.SpawnCount;

        if (iPoolCount < iSpawnCount)
            return eNodeState.Failure;

       
        for (int i = 0; i< iSpawnCount; ++i)
            refSpawnInfo.Weapon.Fire(_refBB.TargetTr.position, _refBB.TargetTr);

        return eNodeState.Success;
    }
}

