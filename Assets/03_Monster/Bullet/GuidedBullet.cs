using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*///////////////////////////////////////////
              GuidedBullet
기능 : 타겟을 따라가는 총알
 *///////////////////////////////////////////

public class GuidedBullet : Bullet
{
    private GameObject m_refTarget;
    private void SetTarget(Player _refPlayer) { m_refTarget = _refPlayer.gameObject; }

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
        //m_refTarget 방향으로 추격
        if (m_refTarget == null)
            return;

        transform.LookAt(m_refTarget.transform.position);
    }
    public override void SetAttack(SOAttackInfo _refAttackInfo)
    {
        base.SetAttack(_refAttackInfo);
    }

    //Trigger
    protected override void AttackMonster(Collider other)
    {
        base.AttackMonster(other);
    }

  
}
