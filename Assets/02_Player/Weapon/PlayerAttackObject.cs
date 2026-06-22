using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttackObject : MonoBehaviour
{
    private Rigidbody m_refRigidbody;
    private SOAttackInfo m_refAttackInfo;
    [SerializeField] eAttackOptionFlag m_eAttackOptFlag = eAttackOptionFlag.Base;

    float m_fCurTime = 0.0f;
    private void Awake()
    {
        m_refRigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        m_fCurTime -= Time.deltaTime;
        if(m_fCurTime <= 0.0f)
            ObjectPool.m_Instance.PushObject(gameObject);
    }

    private void FixedUpdate()
    {
        Vector3 vNextPos = m_refRigidbody.position + transform.forward * m_refAttackInfo.Speed * Time.fixedDeltaTime;
        m_refRigidbody.MovePosition(vNextPos);
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((m_refAttackInfo.HitLayers.value & (1 << other.gameObject.layer)) != 0)
        {
            var iDamageable = other.GetComponent<IDamageable>();
            if (iDamageable != null)
            {
                MakeAttackInfo(out var attack); 
                iDamageable.TakeDamage(attack); // TakeDamage가 tAttackInfo를 받도록 정의되어 있어야 함
            }
        }
    }

    public void SetAttack(SOAttackInfo _refAttackInfo)
    {
        m_fCurTime = _refAttackInfo.AliveTime;
        m_refAttackInfo = _refAttackInfo;
    }


    private void MakeAttackInfo(out tAttackInfo _tAttackInfo)
    {
        _tAttackInfo = new tAttackInfo();

        _tAttackInfo.AttackPower = m_refAttackInfo.AttackPower;
        _tAttackInfo.Damage = m_refAttackInfo.Damage;
        _tAttackInfo.KnockbackForce = m_refAttackInfo.KnockbackForce;
        _tAttackInfo.KnockbackDuration = m_refAttackInfo.KnockbackDuration;
        _tAttackInfo.StunDuration = m_refAttackInfo.StunDuration;
    }
}
