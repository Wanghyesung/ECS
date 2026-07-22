using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                    Laser
목적 : 오브젝트가 바라보는 방향으로 원통형 공격을 하는 오브젝트
 *///////////////////////////////////////////

public class Laser : MonoBehaviour, IAttackObject
{
    [SerializeField] protected AttackInfo m_refAttackInfo;
    protected tShotInfo m_tShotInfo;

    public AttackInfo AttackInfo => m_refAttackInfo;

    protected PoolObject m_refPoolObj;
    private TriggerStayObject m_refTriggerObject;

    [SerializeField] private PoolObject m_refHitEffectPoolObj;

    // Weapon이 부여한 동적 능력치. 발사(AddAttack) 시점마다 덮어써야 하므로 Bullet과 동일하게 참조 대입 방식 사용
    private List<SOBulletAction> m_listWeaponHitActions;


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

        var IDamageable = other.GetComponent<IDamageable>(); 
        if (IDamageable != null)
        {
            m_tShotInfo.LastHitTime = Time.time;
            ++m_tShotInfo.HitCount;
            IDamageable.TakeDamage(m_refAttackInfo, m_tShotInfo);
            RunHitActions();
        }

        if (m_refHitEffectPoolObj != null)
        {
            GameObject refHitEffect = ObjectPool.m_Instance.GetObject(m_refHitEffectPoolObj);
            if (refHitEffect == null)
                return;

            refHitEffect.transform.position = transform.position;
        }
    }

    private void RunHitActions()
    {
        if (m_listWeaponHitActions == null)
            return;

        for (int i = 0; i < m_listWeaponHitActions.Count; ++i)
            m_listWeaponHitActions[i]?.Execute(this);
    }

    // Weapon이 발사 시점마다 호출, 참조를 통째로 덮어씀 (Add 아님) - Pool 재사용 시 중복 실행 방지
    public void SetWeaponHitActions(List<SOBulletAction> _listHitActions)
    {
        m_listWeaponHitActions = _listHitActions;
    }

    private void Update()
    {

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
