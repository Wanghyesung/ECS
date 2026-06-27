using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*///////////////////////////////////////////
              GuidedBullet
기능 : 타겟을 향해가는 총알
 *///////////////////////////////////////////

public class GuidedBullet : Bullet
{
    private Transform m_refTargetTr;

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
        base.FixedUpdate();
    }

    private void Update()
    {
        if (m_refTargetTr == null)
            return;

        transform.LookAt(m_refTargetTr.position);
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
