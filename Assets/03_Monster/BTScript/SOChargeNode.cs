using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                ChargeNode
기능 : 몬스터 차지 오브젝트 실행 및 차지 시간 설정
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_ChargeNode", menuName = "Game/Monster/ActionNode/ChargeNode")]

public class SOChargeNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        SpawnInfo refSpawnInfo = _refBB.CurrentAttackSpawn;
        var refParticleSystem = refSpawnInfo.ChargeParticle; 
        if (refParticleSystem == null)
            return eNodeState.Failure;

        if (refParticleSystem.TryGetComponent<Charge>(out var refCharge))
        {
            refParticleSystem.gameObject.SetActive(true);
            refParticleSystem.gameObject.transform.position = refSpawnInfo.SpawnParticleTransform.position;

            //현재 차지 오브젝트 캐싱
            refCharge.StartCharge(refSpawnInfo.SpawnWaitTime);
            refCharge.SpawnInfo = refSpawnInfo; //공격할 무기 캐싱
            _refBB.CurrentCharge = refCharge;

            _refBB.Owner.StartStateEffect(eStatusEffect.Wait, refSpawnInfo.SpawnWaitTime);
        }

        return eNodeState.Success;
    }
}

