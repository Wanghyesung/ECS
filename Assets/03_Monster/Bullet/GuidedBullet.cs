using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*///////////////////////////////////////////
              GuidedBullet
기능 : 타겟을 향해가는 총알
 *///////////////////////////////////////////

public class GuidedBullet : Bullet
{
    [SerializeField] private Transform m_refTargetTr;

    public void SetTarget(Transform _refTargetTr)
    {
        m_refTargetTr = _refTargetTr;
    }


    protected override void Awake()
    {
        base.Awake();
    }

    protected override void FixedUpdate()
    {
        if (m_refTargetTr != null)
        {
            Vector3 vDir = (m_refTargetTr.position - transform.position).normalized;
            Quaternion qTargetRot = Quaternion.LookRotation(vDir);
            m_refRigidbody.MoveRotation(qTargetRot);
        }

        base.FixedUpdate();
    }

  
    public override void SetAttack(SOAttackInfo _refAttackInfo)
    {
        base.SetAttack(_refAttackInfo);
    }

    protected override void AttackMonster(Collider other)
    {
        base.AttackMonster(other);
    }
}
