using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Weapon;

/*///////////////////////////////////////////
             SOCheckAttackTimeNode
기능 : 몬스터 weapon이 공격 가능한지 체크
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_CheckAttackTimeNode", menuName = "Game/Monster/ActionNode/CheckAttackTimeNode")]
public class SOCheckAttackTimeNode : SONode
{
    [SerializeField] private eWeaponType m_eWeaponType = eWeaponType.None;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (!_refBB.Owner.HashSpawn.TryGetValue(m_eWeaponType, out var listWeapon))
            return eNodeState.Failure;

        for(int i = 0; i<listWeapon.Count; ++i)
        {
            if (listWeapon[i].Weapon.CheckTime() == true)
            {
                _refBB.CurrentAttackSpawn = listWeapon[i];
                return eNodeState.Success;
            }
        }

        return eNodeState.Failure;
    }
}
