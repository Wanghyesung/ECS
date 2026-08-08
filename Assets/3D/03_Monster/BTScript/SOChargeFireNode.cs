using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/*///////////////////////////////////////////
                ChargeNode
기능 : 몬스터 차지 오브젝트 실행 및 차지 시간 설정
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_ChargeFireNode", menuName = "Game/Monster/ActionNode/ChargeFireNode")]

public class SOChargeFireNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        Charge refCurCharge = _refBB.CurrentCharge;
        if (refCurCharge == null || _refBB.TargetTr == null)
            return eNodeState.Failure;

        if (refCurCharge.SpawnInfo == null)
            return eNodeState.Failure;
    
        SpawnInfo refSpawnInfo = refCurCharge.SpawnInfo;
        int iPoolCount = ObjectPool.m_Instance.GetObjectCount(refSpawnInfo.Weapon.FireBulletPrefab);
        int iSpawnCount = refSpawnInfo.SpawnCount;

        if (iPoolCount < iSpawnCount)
            return eNodeState.Failure;

        for (int i = 0; i < iSpawnCount; ++i)
            refSpawnInfo.Weapon.Fire(_refBB.TargetTr.position, _refBB.TargetTr);

        return eNodeState.Success;
    }
}

