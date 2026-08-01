using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*///////////////////////////////////////////
              GuidedBullet
기능 : 타겟을 향해가는 총알
 *///////////////////////////////////////////

public class GuidedBullet : Bullet
{
    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Update()
    {
        if (m_tShotInfo.TargetTr != null)
        {
            Vector3 vDir = (m_tShotInfo.TargetTr.position - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(vDir);
        }

        base.Update();
    }


    public override void SetAttack(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        base.SetAttack(_refAttackInfo, _refShotInfo);
    }

    protected override void AttackMonster(CircleCollider _refOther)
    {
        base.AttackMonster(_refOther);
    }
}
