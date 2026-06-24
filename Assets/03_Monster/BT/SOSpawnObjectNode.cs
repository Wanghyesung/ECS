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
        var listSpawn = _refBB.Owner.ListSpawnObject;
        if (listSpawn == null || listSpawn[_refBB.CurrentAttackIdx] == null)
            return eNodeState.Failure;

        SpawnInfo refSpawnInfo = listSpawn[_refBB.CurrentAttackIdx];
        SOSpawnObjectInfo SOSpawnInfo = refSpawnInfo.SpawnObjectInfo;

        int iPoolCount = ObjectPool.m_Instance.GetObjectCount(SOSpawnInfo.AttackInfo.PoolType);

        int iSpawnCount = refSpawnInfo.SpawnObjectInfo.SpawnCount;
        if (iPoolCount < iSpawnCount)
            return eNodeState.Failure;

        for(int i = 0; i< iSpawnCount; ++i)
        {
            GameObject refObject = ObjectPool.m_Instance.GetObject(SOSpawnInfo.AttackInfo.PoolType);
            _refBB.ListCurAttackObject.Add(refObject);
        }

        return eNodeState.Success;
    }
}

