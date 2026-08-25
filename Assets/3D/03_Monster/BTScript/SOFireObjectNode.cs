using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/*///////////////////////////////////////////
             SOFireObjectNode
기능 : Weapon을 이용하여 지정된 위치에 오브젝트를 발사하는 기능
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_FireObjectNode", menuName = "Game/Monster/ActionNode/FireObjectNode")]
public class SOFireObjectNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        SpawnInfo refSpawnInfo = _refBB.CurrentAttackSpawn;
        if (refSpawnInfo == null || _refBB.TargetTr == null)
            return eNodeState.Failure;

        int iPoolCount = ObjectPoolManager.m_Instance.GetObjectCount(refSpawnInfo.Weapon.FireBulletPrefab);
        int iSpawnCount = refSpawnInfo.SpawnCount;

        if (iPoolCount < iSpawnCount)
            return eNodeState.Failure;

       
        for (int i = 0; i< iSpawnCount; ++i)
            refSpawnInfo.Weapon.Fire(_refBB.TargetTr.position, _refBB.TargetTr);

        return eNodeState.Success;
    }
}

