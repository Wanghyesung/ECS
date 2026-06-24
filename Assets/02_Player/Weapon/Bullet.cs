using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PoolObject;

/*///////////////////////////////////////////
                SelectNode
기능 : 몬스터, 플레이어가 쏘는 기본 공격 오브젝트
 *///////////////////////////////////////////

public class Bullet : MonoBehaviour
{
    private Rigidbody m_refRigidbody;

    [SerializeField] protected SOAttackInfo m_refAttackInfo;
    private PoolObject m_refPoolObj;
    private TriggerObject m_refTriggerObject;

    [SerializeField] ePoolType m_eHitEffectType;

    protected float m_fMoveSpeed = 0.0f;  //동적

    protected virtual void Awake()
    {
        m_refRigidbody = GetComponent<Rigidbody>();
        m_refPoolObj = GetComponent<PoolObject>();
        m_refTriggerObject = GetComponent<TriggerObject>();
    }

    private void OnEnable()
    {
        if(m_refTriggerObject != null)
            m_refTriggerObject.OnHitTargetEnter += AttackMonster;
    }
    private void OnDisable()
    {
        if(m_refTriggerObject != null)
            m_refTriggerObject.OnHitTargetEnter -= AttackMonster;
    }

    protected virtual void Update()
    {

    }

    protected virtual void FixedUpdate()
    {
        Vector3 vNextPos = m_refRigidbody.position + transform.forward * m_fMoveSpeed * Time.fixedDeltaTime;
        m_refRigidbody.MovePosition(vNextPos);
    }

    
    //콜라이더에 걸렸을 때 호출
    protected virtual void AttackMonster(Collider other)
    {
        var iDamageable = other.GetComponent<IDamageable>();
        if (iDamageable != null)
        {
            MakeAttackInfo(out var attack);
            iDamageable.TakeDamage(attack);
        }

        //TODO : 나중에 EffectSO까지 따로 만들어서 확인하는걸로
        if (m_eHitEffectType != ePoolType.None)
        {
            GameObject refHitEffect = ObjectPool.m_Instance.GetObject(m_eHitEffectType);
            refHitEffect.transform.position = transform.position;
        }

        ObjectPool.m_Instance.PushObject(gameObject);
    }

    public virtual void SetAttack(SOAttackInfo _refAttackInfo, Vector3 _vDir)
    {
        m_refAttackInfo = _refAttackInfo;
        m_fMoveSpeed = _refAttackInfo.Speed;
        m_refPoolObj?.SetAliveTime(_refAttackInfo.AliveTime);

        SetOption(_vDir);
    }

    public virtual void SetAttack(SOAttackInfo _refAttackInfo)
    {
        m_refAttackInfo = _refAttackInfo;
        m_fMoveSpeed = _refAttackInfo.Speed;
        m_refPoolObj?.SetAliveTime(_refAttackInfo.AliveTime);
    }


    protected virtual void SetOption(Vector3 _vDir)
    {
        transform.LookAt(_vDir);
    }


    protected void MakeAttackInfo(out tAttackInfo _tAttackInfo)
    {
        _tAttackInfo = new tAttackInfo();

        _tAttackInfo.AttackPower = m_refAttackInfo.AttackPower;
        _tAttackInfo.Damage = m_refAttackInfo.Damage;
        _tAttackInfo.KnockbackForce = m_refAttackInfo.KnockbackForce;
        _tAttackInfo.KnockbackDuration = m_refAttackInfo.KnockbackDuration;
        _tAttackInfo.StunDuration = m_refAttackInfo.StunDuration;
    }
}
