using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/*///////////////////////////////////////////
                IAttackObject
목적 : 공격 오브젝트라면 반드시 구현해야한다는 약속
 *///////////////////////////////////////////
public interface IAttackObject
{
    public void SetAttack(AttackInfo _refAttackInfo, tShotInfo _refShotInfo);

    // SOBulletAction.Execute가 Bullet 상속 여부와 무관하게(Laser 등) 동작하도록 노출.
    // transform은 MonoBehaviour가 이미 제공하므로 구현체에서 별도 작성 불필요
    public AttackInfo AttackInfo { get; }
    public Transform transform { get; }

    // 명중은 Bullet/Laser 등 구현 방식과 무관하게 공통으로 존재하는 이벤트라 인터페이스에 둠
    // (도착/Arrive는 Laser에 없는 개념이라 Bullet 쪽에만 유지).
    public void SetWeaponHitActions(List<SOBulletAction> _listHitActions);
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
    protected CircleCollider m_refCircleCollider;

    // BulletAction 등 외부에서 이 총알을 쐈던 AttackInfo를 그대로 재사용해야 할 때 참조
    public AttackInfo AttackInfo => m_refAttackInfo;

    // protected: Missiles/GuidedBullet이 UpdateLine()을 오버라이드해서 직접 접근해야 함
    [FormerlySerializedAs("m_refLineInfo")]
    [SerializeField] protected BulletLineDrawer m_refLineDrawer; // 볼렛 예고선 담당 (일반 클래스라 인스펙터에 필드가 그대로 펼쳐짐)

    [SerializeField] private PoolObject m_refHitEffectObj;
    // 프리팹 고유 동작. 명중/AliveTime 만료로 풀에 반납되는 시점(=도착)에 실행. 인스펙터에서 조합, 런타임에 안 건드림
    [SerializeField] private SOBulletAction[] m_arrArriveActions;
    // 프리팹 고유 동작. 실제로 대상에 데미지를 입힌 순간(=명중)에만 실행. 인스펙터에서 조합, 런타임에 안 건드림
    [SerializeField] private SOBulletAction[] m_arrHitActions;

    // Weapon이 부여한 동적 능력치. 풀에서 재사용되는 인스턴스이므로 Add로 누적하면 안 되고,
    // Weapon이 발사할 때마다 SetWeaponArriveActions/SetWeaponHitActions로 항상 통째로 덮어써야 중복 실행을 막을 수 있음
    private List<SOBulletAction> m_listWeaponArriveActions;
    private List<SOBulletAction> m_listWeaponHitActions;

    // 이동 계산 매니저(BulletMoveManager 등) 안에서 이 총알이 몇 번째 슬롯인지
    // (풀 생애주기 중 Awake에서 딱 한 번만 배정). 하위 클래스도 DeactivateMoveJob 등에서 써야 해서 protected
    protected int m_iMoveManagerIndex = -1;

    // 히트 이펙트(ParticleSystem) 동시 재생 상한 - 교전이 몰리면 한 프레임에 수십 건씩 명중이
    // 겹칠 수 있는데, 명중마다 무조건 이펙트를 재생하면 동시에 살아있는 ParticleSystem
    // 컴포넌트 수가 급증해 ParticleSystem.Update 비용이 튄다(프로파일러로 확인됨). 총알 종류와
    // 무관하게 전역으로 공유해야 의미가 있어서 모든 Bullet 인스턴스가 공유하는 static으로 둔다
    private const int MAX_HIT_EFFECT_PER_FRAME = 8;
    private static int s_iHitEffectCountThisFrame = 0;
    private static int s_iHitEffectFrame = -1;

    // 이번 프레임에 히트 이펙트를 재생해도 되는지 예약 - 프레임이 바뀌면 카운터를 리셋하고,
    // 한도 내면 자리를 예약(true)한다. Update 콜백 없이 호출 시점에 Time.frameCount로만
    // 판단하므로 별도 초기화/해제 로직이 필요 없다
    private static bool TryReserveHitEffectSlot()
    {
        if (Time.frameCount != s_iHitEffectFrame)
        {
            s_iHitEffectFrame = Time.frameCount;
            s_iHitEffectCountThisFrame = 0;
        }

        if (s_iHitEffectCountThisFrame >= MAX_HIT_EFFECT_PER_FRAME)
            return false;

        ++s_iHitEffectCountThisFrame;
        return true;
    }

    protected virtual void Awake()
    {
        m_refRigidbody = GetComponent<Rigidbody>();
        m_refPoolObj = GetComponent<PoolObject>();
        m_refCircleCollider = GetComponent<CircleCollider>();

        m_iMoveManagerIndex = RegisterMoveJob();

    }


    // 이동 Job 매니저에 슬롯을 배정받는다. Missiles/GuidedBullet은 각자 다른 매니저
    // (MissileMoveManager/GuidedMoveManager)에 등록하도록 오버라이드함
    protected virtual int RegisterMoveJob()
    {
        return BulletMoveManager.m_Instance.RegisterPermanent(transform);
    }

    // 발사 시점(SetAttack)에 이동 Job을 활성화. Missiles/GuidedBullet은 타겟 등 추가 정보를
    // 실어야 해서 오버라이드함
    protected virtual void ActivateMoveJob()
    {
        BulletMoveManager.m_Instance.Activate(m_iMoveManagerIndex, m_tShotInfo.Speed);
    }

    // 반납 시점(OnDisable)에 이동 Job을 비활성화
    protected virtual void UnactivateMoveJob()
    {
        if (m_iMoveManagerIndex >= 0)
            BulletMoveManager.m_Instance.Deactivate(m_iMoveManagerIndex);
    }

    protected virtual void OnEnable()
    {
        if(m_refCircleCollider != null)
            m_refCircleCollider.OnHitTargetEnter += Attack;

        if (m_refPoolObj != null)
            m_refPoolObj.OnPush += RunArriveActions;

        m_tShotInfo.HitCount = 0;
    }
    protected virtual void OnDisable()
    {
        if(m_refCircleCollider != null)
            m_refCircleCollider.OnHitTargetEnter -= Attack;

        if (m_refPoolObj != null)
            m_refPoolObj.OnPush -= RunArriveActions;

        m_refLineDrawer?.CutLine();

        UnactivateMoveJob();
    }

    private void RunArriveActions()
    {
        RunActions(m_arrArriveActions);
        RunActions(m_listWeaponArriveActions);
    }

    private void RunHitActions()
    {
        RunActions(m_arrHitActions);
        RunActions(m_listWeaponHitActions);
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

    // Weapon이 발사 시점마다 호출. 참조를 통째로 덮어쓰는 방식이라(Add 아님) Pool 재사용으로 인한
    // 능력치 중복 실행이 없고, 배열을 새로 만들지 않으므로 매 발사마다 GC Alloc도 발생하지 않음.
    // Arrive는 Laser에 없는 개념이라 인터페이스가 아닌 Bullet 전용 메서드로 둠
    public void SetWeaponArriveActions(List<SOBulletAction> _listArriveActions)
    {
        m_listWeaponArriveActions = _listArriveActions;
    }

    public void SetWeaponHitActions(List<SOBulletAction> _listHitActions)
    {
        m_listWeaponHitActions = _listHitActions;
    }


    protected virtual void Attack(BaseCollider _refOther)
    {
        var iDamageable = _refOther.GetComponent<IDamageable>();
        ++m_tShotInfo.HitCount;//TestCode 
        if (iDamageable != null)
        {
            m_tShotInfo.HitPosition = transform.position;
            iDamageable.TakeDamage(m_refAttackInfo, m_tShotInfo);
            RunHitActions();
        }

        if (m_refHitEffectObj != null && TryReserveHitEffectSlot())
        {
            GameObject refHitEffect = ObjectPoolManager.m_Instance.GetObject(m_refHitEffectObj);
            if (refHitEffect != null)
                refHitEffect.transform.position = transform.position;
        }

        if (m_tShotInfo.HitCount >= m_refAttackInfo.MaxHitCount)
            ObjectPoolManager.m_Instance.PushObject(gameObject);

    }

    //방향따라 동적으로 방향 정해주기
    public virtual void SetAttack(AttackInfo _refAttackInfo, tShotInfo _tShotInfo)
    {
        m_refAttackInfo = _refAttackInfo;
        m_tShotInfo = _tShotInfo;
        m_tShotInfo.MoveDir = transform.forward;
        m_refPoolObj?.SetAliveTime(_refAttackInfo.AliveTime);
        ActivateMoveJob();

        UpdateLine();
    }

    // 예고선 방향/거리 계산 - 직선으로만 날아가는 기본 볼렛 기준. 유도탄처럼 곡선으로 휘는 총알은
    // 이 직선 근사가 실제 경로와 안 맞으므로 Missiles/GuidedBullet에서 타겟 방향 기준으로 오버라이드함
    protected virtual void UpdateLine()
    {
        m_refLineDrawer?.SetLine(transform.position, m_tShotInfo.MoveDir, m_tShotInfo.Speed * m_refAttackInfo.AliveTime);
    }


    // Weapon뿐 아니라 BulletAction 등 "총알 생성 주체(Weapon)를 알 수 없는" 코드도 같은 경로로 스폰하게 함
    public static GameObject SpawnAttackObject(PoolObject _refPrefab, Vector3 _vPos, Quaternion _qRot, AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        GameObject refObj = ObjectPoolManager.m_Instance.GetObject(_refPrefab);
        if (refObj == null)
            return null;

        refObj.transform.position = _vPos;
        refObj.transform.rotation = _qRot;

        IAttackObject refAttackObj = refObj.GetComponent<IAttackObject>();
        refAttackObj?.SetAttack(_refAttackInfo, _refShotInfo);

        return refObj;
    }
}
