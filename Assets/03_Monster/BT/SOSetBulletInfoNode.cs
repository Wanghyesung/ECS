using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
             SpawnObjectNode
기능 : 몬스터가 공격 오브젝트를 소환하는 기능
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_SetBulletInfoNode", menuName = "Game/Monster/ActionNode/SetBulletInfo")]
public class SOSetBulletInfoNode : SONode
{
    [SerializeField] private bool SetDirection = false;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        var listAttackObj = _refBB.ListCurAttackObject;
        for(int i = 0; i< listAttackObj.Count; ++i)
        {
            if(listAttackObj[i].TryGetComponent<Bullet>(out var refBullet) == true)
            {
                var listSpawnObj = _refBB.Owner.ListAttackObject;
                SOSpawnObjectInfo SOSpawnObjInfo = listSpawnObj[_refBB.CurrentAttackIdx].SpawnObjectInfo;

                //if(SetDirection == false)
                //    refBullet.SetAttack(SOSpawnObjInfo.AttackInfo);
            }
            else
            {
                return eNodeState.Failure;
            }
        }

        return eNodeState.Success;
    }
}

