using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttackObject : MonoBehaviour
{
    private SOAttackInfo m_refAttackInfo;
    [SerializeField] eAttackOptionFlag m_eAttackOptFlag = eAttackOptionFlag.Base;

    
    // Update is called once per frame
    void Update()
    {
        transform.Translate(Vector3.forward * m_refAttackInfo.Speed * Time.deltaTime);


        Vector3 vPos = transform.position;
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
