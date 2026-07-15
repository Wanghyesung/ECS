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

    // BulletArriveAction 등 외부에서 이 총알을 쐈던 AttackInfo를 그대로 재사용해야 할 때 참조
    public AttackInfo AttackInfo => m_refAttackInfo;

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


    // 풀에서 공격 오브젝트를 꺼내 위치/회전을 세팅하고 SetAttack까지 호출하는 스폰 로직을 한 곳에 모아,
    // Weapon뿐 아니라 BulletArriveAction 등 "총알 생성 주체(Weapon)를 알 수 없는" 코드도 같은 경로로 스폰하게 함
    public static GameObject SpawnAttackObject(PoolObject _refPrefab, Vector3 _vPos, Quaternion _qRot, AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        GameObject refObj = ObjectPool.m_Instance.GetObject(_refPrefab);
        if (refObj == null)
            return null;

        refObj.transform.position = _vPos;
        refObj.transform.rotation = _qRot;

        IAttackObject refAttackObj = refObj.GetComponent<IAttackObject>();
        refAttackObj?.SetAttack(_refAttackInfo, _refShotInfo);

        return refObj;
    }
}
