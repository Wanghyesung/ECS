using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PoolObject;

public class PlayerAttackObject : MonoBehaviour
{
    private Rigidbody m_refRigidbody;

    protected SOAttackInfo m_refAttackInfo;
    private PoolObject m_refPoolObj;

    [SerializeField] ePoolType m_eHitEffectType;


    protected float m_fMoveSpeed = 0.0f;  //동적
  
    protected virtual void Awake()
    {
        m_refRigidbody = GetComponent<Rigidbody>();
        m_refPoolObj = GetComponent<PoolObject>();
    }

 
    protected virtual void Update()
    {
        
    }


    protected virtual void FixedUpdate()
    {
        Vector3 vNextPos = m_refRigidbody.position + transform.forward * m_fMoveSpeed * Time.fixedDeltaTime;
        m_refRigidbody.MovePosition(vNextPos);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if ((m_refAttackInfo.HitLayers.value & (1 << other.gameObject.layer)) != 0)
        {
            var iDamageable = other.GetComponent<IDamageable>();
            if (iDamageable != null)
            {
                //m_refHitEffect.Play();

                MakeAttackInfo(out var attack); 
                iDamageable.TakeDamage(attack);
            }

            ObjectPool.m_Instance.PushObject(gameObject);
                
            //나중에 EffectSO까지 따로 만들어서 확인하는걸로
            if (m_eHitEffectType != ePoolType.None)
            {
                GameObject refHitEffect = ObjectPool.m_Instance.GetObject(m_eHitEffectType);
                refHitEffect.transform.position = transform.position;
            }
        }
    }

    public virtual void SetAttack(SOAttackInfo _refAttackInfo, Vector3 _vDir)
    {
        m_refAttackInfo = _refAttackInfo;
        m_fMoveSpeed = _refAttackInfo.Speed;
        m_refPoolObj?.SetPushTime(_refAttackInfo.AliveTime);

        SetOption(_vDir);
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
