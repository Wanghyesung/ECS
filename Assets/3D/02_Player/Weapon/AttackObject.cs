using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
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

    [Header("Damage Alive Time")]
    // 콜라이더(=판정)가 켜져 있는 시간. VFX 수명(PoolObject.AliveTime)과 분리해서 "이미지는 남고 데미지는 폭발 순간만".
    // 0 미만(기본 -1)이면 예약을 걸지 않아 AliveTime 내내 판정 (기존 동작)
    [SerializeField] private float m_fDamageAliveTime = -1f;

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

    private IDisposable m_disposableHit;

    private void OnEnable()
    {
        if (m_refCircleCollider != null)
            m_disposableHit = m_refCircleCollider.OnHitTargetEnter.Subscribe(AttackMonster);
    }

    private void OnDisable()
    {
        m_disposableHit?.Dispose();
    }
    private void AttackMonster(BaseCollider _refOther)
    {
        // MaxHitCount를 채우면 데미지만 멈추고 오브젝트는 그대로 둔다 - Bullet처럼 즉시 풀에
        // 반납하면 폭발 VFX가 AliveTime 전에 끊기기 때문. 수명 종료는 PoolObject.AliveTime이 담당
        if (m_tShotInfo.HitCount >= m_refAttackInfo.MaxHitCount)
            return;

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
        //m_refPoolObj.SetAliveTime(m_refAttackInfo.AliveTime);

        m_tShotInfo = _refShotInfo;
        m_tShotInfo.HitCount = 0;

        // 이전 생애에서 CloseCollider가 꺼놨을 수 있으니 다시 켬 → BaseCollider.OnEnable → ColliderManager.Activate
        m_refCircleCollider.enabled = true;
        if (m_fDamageAliveTime >= 0f)
            CloseCollider(this.GetCancellationTokenOnDestroy()).Forget();
    }

    public void SetScale(float _fRadius)
    {
        transform.localScale = Vector3.one * _fRadius;
    }

    // 스포너(SOSpawnAttackObject)가 SetAttack 직후 호출. m_refAttackInfo는 풀 재사용 간 유지되므로
    // 스폰마다 반드시 다시 세팅해야 이전 값이 남지 않는다
    public void SetMaxHitCount(int _iMaxHitCount)
    {
        m_refAttackInfo.MaxHitCount = _iMaxHitCount;
    }
    public void SetWeaponHitActions(List<SOBulletAction> _listHitActions)
    {
        m_listWeaponHitActions = _listHitActions;
    }

    // m_fDamageAliveTime 뒤 콜라이더만 끔 → BaseCollider.OnDisable → ColliderManager.UnActivate. 오브젝트 자체는 남아 VFX 계속 재생
    private async UniTaskVoid CloseCollider(CancellationToken _ct)
    {
        // SetAliveTime(++Generation) 이후에 캡처. 대기 중 반납·재사용되면 값이 달라져 옛 예약이 새 생애의 콜라이더를 끄지 않음
        // (ObjectPoolManager의 tTimeData.iGeneration 가드와 같은 방식)
        int iGeneration = m_refPoolObj.Generation;

        await UniTask.Delay(TimeSpan.FromSeconds(m_fDamageAliveTime), cancellationToken: _ct);

        if (m_refPoolObj.Generation != iGeneration)
            return;

        m_refCircleCollider.enabled = false;
    }
}
