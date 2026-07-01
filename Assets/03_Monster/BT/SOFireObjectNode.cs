using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/*///////////////////////////////////////////
             SOFireObjectNode
��� : ���Ͱ� ���� �Ѿ��� ��� ���
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_FireObjectNode", menuName = "Game/Monster/ActionNode/FireObjectNode")]
public class SOFireObjectNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        SpawnInfo refSpawnInfo = _refBB.CurrentAttackSpawn;
        if (refSpawnInfo == null || _refBB.TargetTr == null)
            return eNodeState.Failure;


        int iPoolCount = ObjectPool.m_Instance.GetObjectCount(refSpawnInfo.Weapon.FireBulletPrefab);
        int iSpawnCount = refSpawnInfo.SpawnCount;

        if (iPoolCount < iSpawnCount)
            return eNodeState.Failure;

       
        for (int i = 0; i< iSpawnCount; ++i)
            refSpawnInfo.Weapon.Fire(_refBB.TargetTr.position, _refBB.TargetTr);

        return eNodeState.Success;
    }
}

