using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Laser : MonoBehaviour, IAttackObject
{
    [SerializeField] protected AttackInfo m_refAttackInfo;

    protected PoolObject m_refPoolObj;
    private TriggerObject m_refTriggerObject;

    [SerializeField] ePoolType m_eHitEffectType;


    protected virtual void Awake()
    {
        m_refPoolObj = GetComponent<PoolObject>();
        m_refTriggerObject = GetComponent<TriggerObject>();
    }

    private void OnEnable()
    {
        if (m_refTriggerObject != null)
            m_refTriggerObject.OnHitTargetEnter += AttackMonster;
    }
    private void OnDisable()
    {
        if (m_refTriggerObject != null)
            m_refTriggerObject.OnHitTargetEnter -= AttackMonster;
    }


    //콜라이더에 걸렸을 때 호출
    protected virtual void AttackMonster(Collider other)
    {
        var iDamageable = other.GetComponent<IDamageable>();
        if (iDamageable != null)
        {
            //iDamageable.TakeDamage(1);
        }

        if (m_eHitEffectType != ePoolType.None)
        {
            GameObject refHitEffect = ObjectPool.m_Instance.GetObject(m_eHitEffectType);
            if (refHitEffect == null)
                return;

            refHitEffect.transform.position = transform.position;
        }

    }

    public virtual void SetAttack(AttackInfo _refAttackInfo)
    {
        m_refAttackInfo = _refAttackInfo;
        m_refPoolObj?.SetAliveTime(_refAttackInfo.AliveTime);
        m_refTriggerObject?.SetTriggerMask(_refAttackInfo.HitLayers);

        transform.LookAt(_refAttackInfo.TargetPos);
    }
}
