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

    protected override void FixedUpdate()
    {
        if (m_tShotInfo.TargetTr != null)
        {
            Vector3 vDir = (m_tShotInfo.TargetTr.position - transform.position).normalized;
            Quaternion qTargetRot = Quaternion.LookRotation(vDir);
            m_refRigidbody.MoveRotation(qTargetRot);
        }

        base.FixedUpdate();
    }


    public override void SetAttack(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        base.SetAttack(_refAttackInfo, _refShotInfo);
    }

    protected override void AttackMonster(Collider other)
    {
        base.AttackMonster(other);
    }
}
