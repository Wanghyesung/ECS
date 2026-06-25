using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuidedBullet : Bullet
{
    private GameObject m_refTarget; 

    protected override void Awake()
    {
        base.Awake();
    }

    
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
    }


    protected override void AttackMonster(Collider other)
    {
        base.AttackMonster(other);
    }

   
}
