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
    [SerializeField] private int m_iSpawnCount;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        var listSpawn = _refBB.Owner.ListSpawnObject;
        if (listSpawn == null || listSpawn[_refBB.CurrentAttackIdx] == null)
            return eNodeState.Failure;

        PoolObject refPoolObj = listSpawn[_refBB.CurrentAttackIdx].AttackObject;
        int iCurCount = ObjectPool.m_Instance.GetObjectCount(refPoolObj.PoolType);

        if (iCurCount < m_iSpawnCount)
            return eNodeState.Failure;

        for(int i = 0; i< m_iSpawnCount; ++i)
        {
            GameObject refObject = ObjectPool.m_Instance.GetObject(refPoolObj.PoolType);
            _refBB.ListCurAttackObject.Add(refObject);
        }

        return eNodeState.Success;
    }
}

