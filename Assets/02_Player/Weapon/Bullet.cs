using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                IAttackObject
목적 : 공격 오브젝트라면 반드시 구현해야한다는 약속
 *///////////////////////////////////////////
public interface IAttackObject
{
    public void SetAttack(AttackInfo _refAttackInfo, tShotInfo _refShotInfo);
}

/*///////////////////////////////////////////
                  Bullet
목적 : 몬스터, 플레이어 등의 기본 발사 오브젝트
 *///////////////////////////////////////////
[RequireComponent(typeof(PoolObject))]

public class Bullet : MonoBehaviour, IAttackObject
{
    protected Rigidbody m_refRigidbody;

    protected AttackInfo m_refAttackInfo;
    protected tShotInfo m_tShotInfo;

    protected PoolObject m_refPoolObj;
    protected ITriggerable m_refTriggerObject;

    [SerializeField] private PoolObject m_refHitEffectObj;

    // 명중/AliveTime 만료로 풀에 반납되는 시점(=도착)에 실행할 로직들. 프리팹별로 인스펙터에서 조합
    [SerializeField] private SOBulletArriveAction[] m_arrArriveActions;


    protected virtual void Awake()
    {
        m_refRigidbody = GetComponent<Rigidbody>();
        m_refPoolObj = GetComponent<PoolObject>();
        m_refTriggerObject = GetComponent<ITriggerable>();
    }

    protected virtual void OnEnable()
    {
        if(m_refTriggerObject != null)
            m_refTriggerObject.OnHitTargetEnter += AttackMonster;

        if (m_refPoolObj != null)
            m_refPoolObj.OnPush += RunArriveActions;

        m_tShotInfo.HitCount = 0;
    }
    protected virtual void OnDisable()
    {
        if(m_refTriggerObject != null)
            m_refTriggerObject.OnHitTargetEnter -= AttackMonster;

        if (m_refPoolObj != null)
            m_refPoolObj.OnPush -= RunArriveActions;
    }

    private void RunArriveActions()
    {
        if (m_arrArriveActions == null)
            return;

        for (int i = 0; i < m_arrArriveActions.Length; ++i)
            m_arrArriveActions[i]?.Execute(this);
    }
    

    protected virtual void FixedUpdate()
    {
        Vector3 vNextPos = m_refRigidbody.position + transform.forward * m_tShotInfo.Speed * Time.fixedDeltaTime;
        m_refRigidbody.MovePosition(vNextPos);

    }

    protected virtual void AttackMonster(Collider other)
    {
        var iDamageable = other.GetComponent<IDamageable>();
        if (iDamageable != null)
        {
            if (m_tShotInfo.HitCount >= m_refAttackInfo.MaxHitCount)
                return;

            ++m_tShotInfo.HitCount;
            m_tShotInfo.HitPosition = transform.position;
            iDamageable.TakeDamage(m_refAttackInfo, m_tShotInfo);
        }

        if (m_refHitEffectObj != null)
        {
            GameObject refHitEffect = ObjectPool.m_Instance.GetObject(m_refHitEffectObj);
            if (refHitEffect == null)
                return;

            refHitEffect.transform.position = transform.position;
        }

        ObjectPool.m_Instance.PushObject(gameObject);
    }
    //방향따라 동적으로 방향 정해주기
    public virtual void SetAttack(AttackInfo _refAttackInfo, tShotInfo _tShotInfo)
    {
        m_refAttackInfo = _refAttackInfo;
        m_tShotInfo = _tShotInfo;
        m_tShotInfo.MoveDir = transform.forward;
        m_refPoolObj?.SetAliveTime(_refAttackInfo.AliveTime);
        m_refTriggerObject.LayerMask = _refAttackInfo.HitLayers;
    }
}
