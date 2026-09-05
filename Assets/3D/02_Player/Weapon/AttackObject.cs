using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                AttackObject
목적 : 제자리에서 범위 판정만 하는 공격 오브젝트 (예: 폭발). Bullet과 달리 이동이 없어
       FixedUpdate 이동 로직을 갖지 않음..
 *///////////////////////////////////////////

[RequireComponent(typeof(PoolObject))]
[RequireComponent(typeof(CircleCollider))]

public class AttackObject : MonoBehaviour, IAttackObject
{
    [SerializeField] private SOPoolData m_refHitEffectObj;

    // 프리팹 고유 동작. 인스펙터에서 조합, 런타임에 안 건드림 (Bullet과 동일한 관례)
    [SerializeField] private SOBulletAction[] m_arrHitActions;

    private AttackInfo m_refAttackInfo = new AttackInfo();
    public AttackInfo AttackInfo => m_refAttackInfo;

    private PoolObject m_refPoolObj;
    private CircleCollider m_refCircleCollider;
    private tShotInfo m_tShotInfo;

    // FeatureSO 등 외부에서 부여한 명중 시 능력치. Weapon.ApplyGrantedActions와 동일한 계약
    private List<SOBulletAction> m_listWeaponHitActions;

    private void Awake()
    {
        m_refPoolObj = GetComponent<PoolObject>();
        m_refCircleCollider = GetComponent<CircleCollider>();
    }

    private void OnEnable()
    {
        if (m_refCircleCollider != null)
            m_refCircleCollider.OnHitTargetEnter += AttackMonster;
    }

    private void OnDisable()
    {
        if (m_refCircleCollider != null)
            m_refCircleCollider.OnHitTargetEnter -= AttackMonster;
    }
    private void AttackMonster(BaseCollider _refOther)
    {
        var iDamageable = _refOther.GetComponent<IDamageable>();
        if (iDamageable != null)
        {
            ++m_tShotInfo.HitCount;
            m_tShotInfo.HitPosition = transform.position;
            iDamageable.TakeDamage(m_refAttackInfo, m_tShotInfo);

            RunActions(m_arrHitActions);
            RunActions(m_listWeaponHitActions);
        }

        if (m_refHitEffectObj != null)
        {
            GameObject refHitEffect = ObjectPoolManager.m_Instance.GetObject(m_refHitEffectObj);
            if (refHitEffect != null)
                refHitEffect.transform.position = transform.position;
        }
    }

    private void RunActions(SOBulletAction[] _arrActions)
    {
        if (_arrActions == null)
            return;

        for (int i = 0; i < _arrActions.Length; ++i)
            _arrActions[i]?.Execute(this);
    }

    private void RunActions(List<SOBulletAction> _listActions)
    {
        if (_listActions == null)
            return;

        for (int i = 0; i < _listActions.Count; ++i)
            _listActions[i]?.Execute(this);
    }


    public void SetAttack(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        m_refAttackInfo.Damage = _refAttackInfo.Damage; //SO로 추가 딜 가능
        m_refPoolObj.SetAliveTime(m_refAttackInfo.AliveTime);

        m_tShotInfo = _refShotInfo;
        m_tShotInfo.HitCount = 0;
    }

    public void SetScale(float _fRadius)
    {
        transform.localScale = Vector3.one * _fRadius;
    }
    public void SetWeaponHitActions(List<SOBulletAction> _listHitActions)
    {
        m_listWeaponHitActions = _listHitActions;
    }
}
