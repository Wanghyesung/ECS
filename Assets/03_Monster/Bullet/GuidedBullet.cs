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
        if (m_refAttackInfo.TargetTrasnform != null)
        {
            Vector3 vDir = (m_refAttackInfo.TargetTrasnform.position - transform.position).normalized;
            Quaternion qTargetRot = Quaternion.LookRotation(vDir);
            m_refRigidbody.MoveRotation(qTargetRot);
        }

        base.FixedUpdate();
    }

  
    public override void SetAttack(AttackInfo _refAttackInfo)
    {
        base.SetAttack(_refAttackInfo);
    }

    protected override void AttackMonster(Collider other)
    {
        base.AttackMonster(other);
    }
}
