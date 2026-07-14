using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Laser : MonoBehaviour, IAttackObject
{
    [SerializeField] protected AttackInfo m_refAttackInfo;
    protected tShotInfo m_tShotInfo;

    protected PoolObject m_refPoolObj;
    private TriggerStayObject m_refTriggerObject;

    [SerializeField] private PoolObject m_refHitEffectPoolObj;


    protected virtual void Awake()
    {
        m_refPoolObj = GetComponent<PoolObject>();
        m_refTriggerObject = GetComponent<TriggerStayObject>();
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

    protected virtual void AttackMonster(Collider other)
    {
        if ((Time.time - m_tShotInfo.LastHitTime) < m_refAttackInfo.HitStep)
            return;
        if (m_tShotInfo.HitCount >= m_refAttackInfo.MaxHitCount)
            return;

        var IDamageable = other.GetComponent<IDamageable>(); //이거 캐싱하는게 좋을 것 같음
        if (IDamageable != null)
        {
            m_tShotInfo.LastHitTime = Time.time;
            ++m_tShotInfo.HitCount;
            IDamageable.TakeDamage(m_refAttackInfo, m_tShotInfo);
        }

        if (m_refHitEffectPoolObj != null)
        {
            GameObject refHitEffect = ObjectPool.m_Instance.GetObject(m_refHitEffectPoolObj);
            if (refHitEffect == null)
                return;

            refHitEffect.transform.position = transform.position;
        }
    }

    private void Update()
    {
        //만약 내가 지워져서 자식 Laser까지 지워지게 된다면 해당 laser는 오브젝트 풀로 돌아가지 못하게 됨
        //때문에 매 프레임 부모를 기준으로 회전 
        transform.rotation = m_refAttackInfo.Owner.transform.rotation;
        transform.position = m_refAttackInfo.Owner.transform.position;
    }

   

    public virtual void SetAttack(AttackInfo _refAttackInfo, tShotInfo _tShotInfo)
    {
        m_refAttackInfo = _refAttackInfo;
        m_tShotInfo = _tShotInfo;
        m_tShotInfo.MoveDir = transform.forward;
        m_refPoolObj?.SetAliveTime(_refAttackInfo.AliveTime);
        m_refTriggerObject.LayerMask = _refAttackInfo.HitLayers;


        m_tShotInfo.HitCount = 0;
        m_tShotInfo.LastHitTime = Time.time;
    }
}
