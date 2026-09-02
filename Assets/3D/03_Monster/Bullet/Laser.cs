using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/*///////////////////////////////////////////
                    Laser
목적 : 오브젝트가 바라보는 방향으로 원통형 공격을 하는 오브젝트.
       PhysX 없이 ColliderManager.RaycastAllMask(레이 + 반경)로 판정한다 - 몬스터가
       PhysX Collider를 안 갖고 있어서(자체 CircleCollider만 사용) 예전 TriggerStayObject
       (PhysX OnTriggerStay) 기반으로는 몬스터를 못 맞혔음.

       지속형(HitStep > 0, 기존 BossLaser)과 관통형(HitStep <= 0, 신규)을 한 클래스로
       처리한다 - 둘 다 결국 "레이캐스트 쿼리 + HitStep/MaxHitCount 전역 카운터"로 표현
       가능해서 판정 파이프라인이 같기 때문. TelegraphDuration(기본값 0)이 있으면 그만큼
       예고선만 보여주다가 판정을 시작한다.
 *///////////////////////////////////////////

public class Laser : MonoBehaviour, IAttackObject
{
    [SerializeField] protected AttackInfo m_refAttackInfo;

    [SerializeField] private PoolObject m_refHitEffectPoolObj;
    [SerializeField] private BulletLineDrawer m_refLineDrawer;   // 예고선 - optional, 없으면 스킵
    [SerializeField] private float m_fRange = 20f;
    [SerializeField] private float m_fLaserRadius = 0.5f;        // 판정 두께(레이 자체 반경)

    protected tShotInfo m_tShotInfo;
    protected PoolObject m_refPoolObj;

    // Weapon이 부여한 동적 능력치. 발사(SetAttack) 시점마다 덮어써야 하므로 Bullet과 동일하게 참조 대입 방식 사용
    private List<SOBulletAction> m_listWeaponHitActions;

    private CancellationTokenSource m_cts;   // 풀링 안전 - GetCancellationTokenOnDestroy는 SetActive(false)로 취소 안 됨

    // 매 쿼리마다 재사용 - GC Alloc 없음 (지속형은 매 프레임 호출될 수 있어서 중요)
    private readonly List<CircleCollider> m_listHitBuffer = new List<CircleCollider>(16);

    private bool m_bJudging;   // 예고선 끝나고 실제 판정 구간에 들어왔는지

    public AttackInfo AttackInfo => m_refAttackInfo;

    protected virtual void Awake()
    {
        m_refPoolObj = GetComponent<PoolObject>();
    }

    private void OnEnable()
    {
        m_cts = new CancellationTokenSource();
    }

    private void OnDisable()
    {
        m_cts.Cancel();
        m_cts.Dispose();

        m_bJudging = false;
        m_refLineDrawer?.CutLine();
    }

    private void Update()
    {
        transform.rotation = m_refAttackInfo.Owner.transform.rotation;
        transform.position = m_refAttackInfo.Owner.transform.position;

        if (m_bJudging == false || m_tShotInfo.HitCount >= m_refAttackInfo.MaxHitCount)
            return;

        DoHitCheck();   // 감지는 매 프레임(예전 OnTriggerStay와 동등한 빈도) 

        if (m_refAttackInfo.HitStep <= 0f)
            m_bJudging = false;   // 관통형: 한 번 쐈으면 끝
    }

    public virtual void SetAttack(AttackInfo _refAttackInfo, tShotInfo _tShotInfo)
    {
        m_refAttackInfo = _refAttackInfo;
        m_tShotInfo = _tShotInfo;
        m_tShotInfo.MoveDir = transform.forward;
        m_tShotInfo.HitCount = 0;
        m_tShotInfo.LastHitTime = -Mathf.Infinity;
        m_bJudging = false;

        m_refPoolObj?.SetAliveTime(_refAttackInfo.AliveTime);   // 자동 반납 전담 (Bullet.cs와 동일 패턴, 별도 타이머 없음)

        if (_refAttackInfo.LineDuration > 0f)
        {
            m_refLineDrawer?.SetLine(transform.position, transform.forward, m_fRange);
            TelegraphThenJudge(m_cts.Token).Forget();
        }
        else
        {
            m_bJudging = true;   // 예고선 없으면 이번 프레임부터 바로 판정 (기존 BossLaser와 동일)
        }
    }

    // Weapon이 발사 시점마다 호출, 참조를 통째로 덮어씀 (Add 아님) - Pool 재사용 시 중복 실행 방지
    public void SetWeaponHitActions(List<SOBulletAction> _listHitActions)
    {
        m_listWeaponHitActions = _listHitActions;
    }

    private async UniTaskVoid TelegraphThenJudge(CancellationToken _token)
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(m_refAttackInfo.LineDuration), cancellationToken: _token);

        m_refLineDrawer?.CutLine();
        m_bJudging = true;
    }

    private void DoHitCheck()
    {
        ColliderManager.m_Instance.RaycastMask(
            transform.position, transform.forward, m_fRange, m_fLaserRadius,
            m_refAttackInfo.HitLayers, m_listHitBuffer);

        if (m_listHitBuffer.Count == 0)
            return;

        // 감지는 항상 하되(위 Update 참고), 데미지 적용은 여전히 HitStep으로 스로틀 - 예전 OnTriggerStay+HitStep과 동일한 의미
        bool bCanApply = m_refAttackInfo.HitStep <= 0f
            || (Time.time - m_tShotInfo.LastHitTime) >= m_refAttackInfo.HitStep;

        if (bCanApply == false)
            return;

        int iRemaining = m_refAttackInfo.MaxHitCount - m_tShotInfo.HitCount;
        int iMaxCount = Mathf.Min(m_listHitBuffer.Count, iRemaining);

        for (int i = 0; i < iMaxCount; ++i)
        {
            var refDamageable = m_listHitBuffer[i].GetComponent<IDamageable>();
            if (refDamageable == null)
                continue;

            m_tShotInfo.HitPosition = m_listHitBuffer[i].Center;
            m_tShotInfo.LastHitTime = Time.time;
            ++m_tShotInfo.HitCount;
            refDamageable.TakeDamage(m_refAttackInfo, m_tShotInfo);
            RunHitActions();

            if (m_refHitEffectPoolObj != null)
            {
                GameObject refHitEffect = ObjectPoolManager.m_Instance.GetObject(m_refHitEffectPoolObj);
                if (refHitEffect != null)
                    refHitEffect.transform.position = m_listHitBuffer[i].Center;
            }
        }
    }

    private void RunHitActions()
    {
        if (m_listWeaponHitActions == null)
            return;

        for (int i = 0; i < m_listWeaponHitActions.Count; ++i)
            m_listWeaponHitActions[i]?.Execute(this);
    }
}
