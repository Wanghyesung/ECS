using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/*///////////////////////////////////////////
                NukeMissile
목적 : 핵폭탄 카드의 미사일. 발사 측이 잡아준 시작 위치에서 착탄점(tShotInfo.TargetPos)까지
       직진하는 "이동"만 담당하고,
       착탄 시 무엇이 일어나는지는 ArriveActions(SOBulletAction)로 조합한다 - 전체 처치는
       SOKillAllMonstersAction, 폭발 연출은 기존 SOSpawnExplosionAction.
 *///////////////////////////////////////////

[RequireComponent(typeof(PoolObject))]
public sealed class NukeMissile : MonoBehaviour, IAttackObject
{
    [Header("Fall")]
    [SerializeField] private float m_fFallTime = 2f;

    // 프리팹 고유 착탄 동작. 인스펙터에서 조합, 런타임에 안 건드림 (Bullet/AttackObject과 동일한 관례)
    [SerializeField] private SOBulletAction[] m_arrArriveActions;

    private PoolObject m_refPoolObj;
    private AttackInfo m_refAttackInfo;
    private tShotInfo m_tShotInfo;

    public AttackInfo AttackInfo => m_refAttackInfo;
    public float FallTime => m_fFallTime;

    private void Awake()
    {
        m_refPoolObj = GetComponent<PoolObject>();
    }

    private void OnDisable()
    {
        // 낙하 도중 풀 반납/씬 전환 시 트윈이 비활성 트랜스폼을 계속 밀지 않도록
        transform.DOKill();
    }

    // Bullet.SpawnAttackObject가 호출. 시작 위치는 그쪽에서 이미 잡혀 있고, _refShotInfo.TargetPos = 착탄 지점
    public void SetAttack(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        m_refAttackInfo = _refAttackInfo;
        m_tShotInfo = _refShotInfo;

        // 자동 반납 없이 착탄 후 직접 반납 (프리팹 AliveTime은 0으로 두는 것이 전제)
        FallAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid FallAsync(CancellationToken _token)
    {
        Vector3 vTarget = m_tShotInfo.TargetPos;
        transform.rotation = Quaternion.LookRotation(vTarget - transform.position); // 기수(+Z)를 착탄점 방향으로

        // UNITASK_DOTWEEN_SUPPORT 미정의라 트윈을 await하지 못함 - 같은 시간만큼 unscaled Delay로 대기
        transform.DOMove(vTarget, m_fFallTime).SetEase(Ease.InQuad).SetUpdate(true);
        await UniTask.Delay(TimeSpan.FromSeconds(m_fFallTime), ignoreTimeScale: true, cancellationToken: _token);

        m_tShotInfo.HitPosition = vTarget;
        for (int i = 0; i < m_arrArriveActions.Length; ++i)
            m_arrArriveActions[i]?.Execute(this);

        ObjectPoolManager.m_Instance.PushObject(gameObject);
    }

    // IAttackObject 계약. 핵은 충돌 명중이 없어 무기 부여 명중 액션을 쓸 일이 없음 - 인터페이스 충족용 no-op
    public void SetWeaponHitActions(List<SOBulletAction> _listHitActions) { }
}
