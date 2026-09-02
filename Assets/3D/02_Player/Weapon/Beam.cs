using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                    Beam
목적 : 시작점~끝점 사이를 생성 시점에 딱 한 번만 관통 판정하는 순간 발사형 공격
       오브젝트. 판정(DoHitCheck)은 스폰 즉시 레이로 끝나고, 비주얼은 그와 별개로
       AttackInfo.Speed로 정해진 빠른 속도로 끝점까지 실제 이동하면서
       TrailRenderer가 그 궤적을 자연스럽게 따라가는 잔상(터널링)을 남긴다.
       판정과 이동 도착 여부는 무관 - 이동 중 대상이 움직여도 이미 끝난 판정
       결과는 안 바뀜. Laser(지속형)와 달리 반복 판정이나 예고선이 없다.
 *///////////////////////////////////////////

[RequireComponent(typeof(PoolObject), typeof(TrailRenderer))]
public sealed class Beam : MonoBehaviour, IAttackObject
{
    [SerializeField] private PoolObject m_refHitEffectPoolObj;
    [SerializeField] private float m_fRange = 30f;
    [SerializeField] private float m_fRadius = 0.5f;   // 판정 두께(레이 자체 반경)

    private AttackInfo m_refAttackInfo;
    private tShotInfo m_tShotInfo;
    private PoolObject m_refPoolObj;
    private TrailRenderer m_refTrailRenderer;
    private List<SOBulletAction> m_listWeaponHitActions;

    private Vector3 m_vEndPosition;
    private bool m_bTraveling;

    // 매 발사마다 재사용 - GC Alloc 없음
    private readonly List<CircleCollider> m_listHitBuffer = new List<CircleCollider>(16);

    public AttackInfo AttackInfo => m_refAttackInfo;

    private void Awake()
    {
        m_refPoolObj = GetComponent<PoolObject>();
        m_refTrailRenderer = GetComponent<TrailRenderer>();
    }

    private void Update()
    {
        if (m_bTraveling == false)
            return;

        float fStep = m_refAttackInfo.Speed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, m_vEndPosition, fStep);

        if (transform.position == m_vEndPosition)
        {
            m_bTraveling = false;
            m_refTrailRenderer.emitting = false;   // 도착 후 더 이상 새 점이 안 찍히게 고정, 기존 궤적은 time 경과에 따라 페이드아웃
        }
    }

    public void SetAttack(AttackInfo _refAttackInfo, tShotInfo _tShotInfo)
    {
        m_refAttackInfo = _refAttackInfo;
        m_tShotInfo = _tShotInfo;
        m_tShotInfo.MoveDir = transform.forward;
        m_tShotInfo.HitCount = 0;

        m_refPoolObj.SetAliveTime(_refAttackInfo.AliveTime);   // 자동 반납 전담 (Bullet.cs와 동일 패턴, 별도 타이머 없음)

        DoHitCheck();   // 판정은 스폰 즉시 레이로 완료 - 아래 이동 연출과 무관

        m_refTrailRenderer.Clear();     // 풀 재사용 시 이전 생애의 잔여 궤적 제거
        m_refTrailRenderer.time = _refAttackInfo.AliveTime;   // 라인 렌더링 지속시간 = AliveTime 하나로 통일
        m_refTrailRenderer.emitting = true;

        m_vEndPosition = transform.position + transform.forward * m_fRange;
        m_bTraveling = true;
    }

    // Weapon이 발사 시점마다 호출, 참조를 통째로 덮어씀 (Add 아님) - Pool 재사용 시 중복 실행 방지
    public void SetWeaponHitActions(List<SOBulletAction> _listHitActions)
    {
        m_listWeaponHitActions = _listHitActions;
    }

    private void DoHitCheck()
    {
        ColliderManager.m_Instance.RaycastMask(
            transform.position, transform.forward, m_fRange, m_fRadius,
            m_refAttackInfo.HitLayers, m_listHitBuffer);

        int iLimit = Mathf.Min(m_listHitBuffer.Count, m_refAttackInfo.MaxHitCount);
        for (int i = 0; i < iLimit; ++i)
        {
            var refDamageable = m_listHitBuffer[i].GetComponent<IDamageable>();
            if (refDamageable == null)
                continue;

            m_tShotInfo.HitPosition = m_listHitBuffer[i].Center;
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
