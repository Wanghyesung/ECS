using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PoolObject;


/*///////////////////////////////////////////
                IAttackObject
기능 : 공격 오브젝의 공격정보를 셋팅해야한다는 규약
 *///////////////////////////////////////////
public interface IAttackObject
{
    public void SetAttack(AttackInfo _refAttackInfo);
}

/*///////////////////////////////////////////
                  Bullet
기능 : 몬스터, 플레이어가 쏘는 기본 공격 오브젝트
 *///////////////////////////////////////////

public class Bullet : MonoBehaviour, IAttackObject
{
    protected Rigidbody m_refRigidbody;

    protected AttackInfo m_refAttackInfo;

    protected PoolObject m_refPoolObj;
    protected TriggerObject m_refTriggerObject;

    [SerializeField] private ePoolType m_eHitEffectType;


    protected virtual void Awake()
    {
        m_refRigidbody = GetComponent<Rigidbody>();
        m_refPoolObj = GetComponent<PoolObject>();
        m_refTriggerObject = GetComponent<TriggerObject>();
    }

    protected virtual void OnEnable()
    {
        if(m_refTriggerObject != null)
            m_refTriggerObject.OnHitTargetEnter += AttackMonster;
    }
    protected virtual void OnDisable()
    {
        if(m_refTriggerObject != null)
            m_refTriggerObject.OnHitTargetEnter -= AttackMonster;
    }


    protected virtual void FixedUpdate()
    {
        Vector3 vNextPos = m_refRigidbody.position + transform.forward * m_refAttackInfo.AttackSpeed * Time.fixedDeltaTime;
        m_refRigidbody.MovePosition(vNextPos);
    }

    
    //콜라이더에 걸렸을 때 호출
    protected virtual void AttackMonster(Collider other)
    {
        var iDamageable = other.GetComponent<IDamageable>();
        if (iDamageable != null)
        {
            //iDamageable.TakeDamage(1);
        }

        //TODO : 나중에 EffectSO까지 따로 만들어서 확인하는걸로
        if (m_eHitEffectType != ePoolType.None)
        {
            GameObject refHitEffect = ObjectPool.m_Instance.GetObject(m_eHitEffectType);
            if (refHitEffect == null)
                return;

            refHitEffect.transform.position = transform.position;
        }

        ObjectPool.m_Instance.PushObject(gameObject);
    }

    public virtual void SetAttack(AttackInfo _refAttackInfo)
    {
        m_refAttackInfo = _refAttackInfo;
        m_refPoolObj?.SetAliveTime(_refAttackInfo.AliveTime);
        m_refTriggerObject.SetTriggerMask(_refAttackInfo.HitLayers);

        transform.LookAt(_refAttackInfo.TargetPos);
    }
}
