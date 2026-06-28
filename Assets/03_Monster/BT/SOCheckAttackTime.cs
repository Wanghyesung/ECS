using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Weapon;

/*///////////////////////////////////////////
             SOCheckAttackTimeNode
기능 : 현재 몬스터 무기가 공격할 수 있는지 체크
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_CheckAttackTimeNode", menuName = "Game/Monster/ActionNode/CheckAttackTimeNode")]
public class SOCheckAttackTimeNode : SONode
{
    [SerializeField] private eWeaponType m_eWeaponType = eWeaponType.None;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        var listWeapon = _refBB.Owner.HashSpawn[m_eWeaponType];
        
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
