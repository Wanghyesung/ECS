using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class Weapon : MonoBehaviour
{
    
    public enum eWeaponType
    {
        None,
        Bullet,
        Trace,
        MissileBullet,
        Missile,
        Laser,
        ShotGun,
        End,
    }
    
    
    [SerializeField] private SOAttackInfo m_SOAttackInfo;

    private AttackInfo m_refAttackInfo;

    [SerializeField] private Transform m_refFireTr = null;
    [SerializeField] private ParticleSystem m_refEffectObject;

    private float m_fFireTime = 0.2f;
    private float m_fBaseCooldown = 0.2f;
    private float m_fLastFireTime = -Mathf.Infinity;

    private eWeaponType m_eWeapoonType = eWeaponType.None;
    public eWeaponType WeaponType => m_eWeapoonType;

    public PoolObject FireBulletPrefab => m_SOAttackInfo.PoolPrefab;

    [Header("Weapon Option")]
    [SerializeField] private bool m_bLookTarget = true;

    [Header("Inaccuracy")]
    [SerializeField] private float m_fInaccuracyAngle = 0f; // 조준 방향에서 좌우/상하로 흔들리는 오차 각도

    [Header("Circular Sector Shot")]
    [SerializeField] private int m_iBulletCount = 1; // 1이면 기존처럼 단발
    [SerializeField] private float m_fSpreadAngle = 30f; // 부채꼴(원뿔) 전체 각도

    private const float GOLDEN_ANGLE_DEG = 137.50776f;

    // 레벨업 등으로 획득한 동적 능력치. 총알 프리팹 개수와 무관하게 Weapon(적은 개수) 쪽에 모아두고
    // 발사할 때마다 총알 인스턴스에 참조로 얹어줌 (AddArriveAction/AddHitAction 참고)
    private SOBulletAction[] m_arrArriveActions;
    private SOBulletAction[] m_arrHitActions;

    private void Awake()
    {
        m_refAttackInfo = m_SOAttackInfo.MakeAttackInfo();
        m_refAttackInfo.Owner = gameObject.transform;
        m_fFireTime = m_refAttackInfo.CoolDown;
        m_fBaseCooldown = m_refAttackInfo.CoolDown;
        m_eWeapoonType = m_SOAttackInfo.WeaponType;
        m_fLastFireTime = Time.time;

#if UNITY_EDITOR
        if (m_refAttackInfo == null)
            Debug.Log("공격 SO 에셋설정을 안 함");
        //UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void Fire(Vector3 _vTargetPos, Transform _refTargetTr)
    {
        tShotInfo refShotInfo = new tShotInfo();
        refShotInfo.TargetPos = _vTargetPos;
        refShotInfo.TargetTr = _refTargetTr;
        refShotInfo.Speed = RollSpeed();

        if (m_iBulletCount > 1)
        {
            FireCircularSector(_vTargetPos, refShotInfo);
            return;
        }

        Vector3 vLookDir = _vTargetPos - m_refFireTr.position;
        Quaternion qRot = (m_bLookTarget == true && vLookDir.sqrMagnitude > 0.0001f)
            ? Quaternion.LookRotation(vLookDir)
            : m_refFireTr.rotation;
        qRot = ApplyInaccuracy(qRot);

        GameObject refObj = Bullet.SpawnAttackObject(m_SOAttackInfo.PoolPrefab, m_refFireTr.position, qRot, m_refAttackInfo, refShotInfo);
        if (refObj == null)
            return;

        ApplyGrantedActions(refObj);
        OnBulletFired();
    }

    // 조준 방향(_vTargetPos)을 중심축으로, 반각 m_fSpreadAngle/2인 원뿔 단면에 m_iBulletCount발을
    // 골든 앵글 스파이럴로 균등 분포시켜 3D 부채꼴(샷건 콘) 형태로 발사
    private void FireCircularSector(Vector3 _vTargetPos, tShotInfo _refShotInfo)
    {
        Vector3 vBaseDir = (_vTargetPos - m_refFireTr.position).normalized;
        Vector3 vSpokeAxis = Vector3.Cross(vBaseDir, m_refFireTr.up);
        vSpokeAxis.Normalize();

        float fHalfAngle = m_fSpreadAngle * 0.5f;

        for (int i = 0; i < m_iBulletCount; ++i)
        {
            // fConeAngle: 중심축에서 얼마나 벌어지는지 (sqrt 분포로 원뿔 단면에 균등하게 채움)
            // fSpinAngle: 중심축을 기준으로 몇 도 회전한 스포크에 놓을지 (골든 앵글로 겹치지 않게 배치)
            float fRatio = (i + 0.5f) / m_iBulletCount;
            float fConeAngle = Mathf.Sqrt(fRatio) * fHalfAngle;
            float fSpinAngle = i * GOLDEN_ANGLE_DEG; //i가 증가할 때마다 황금각만큼 계속 회전시키기

            Vector3 vAxis = Quaternion.AngleAxis(fSpinAngle, vBaseDir) * vSpokeAxis;//실제로 회전시킬 대상인 3D 화살표
            Vector3 vDir = Quaternion.AngleAxis(fConeAngle, vAxis) * vBaseDir;

            Quaternion qRot = ApplyInaccuracy(Quaternion.LookRotation(vDir));

            tShotInfo refPelletShotInfo = _refShotInfo;
            refPelletShotInfo.Speed = RollSpeed();

            GameObject refObj = Bullet.SpawnAttackObject(m_SOAttackInfo.PoolPrefab, m_refFireTr.position, qRot, m_refAttackInfo, refPelletShotInfo);
            if (refObj == null)
                continue;

            ApplyGrantedActions(refObj);
            OnBulletFired();
        }
    }


    public void FireAndRotate(Vector3 _vDir, float _fFowardOffset)
    {
        Vector3 vSpawnPos = m_refFireTr.position + (_vDir * _fFowardOffset);
        Quaternion qRot = ApplyInaccuracy(Quaternion.LookRotation(_vDir));

        tShotInfo refShotInfo = new tShotInfo();
        refShotInfo.Speed = RollSpeed();

        GameObject refObj = Bullet.SpawnAttackObject(m_SOAttackInfo.PoolPrefab, vSpawnPos, qRot, m_refAttackInfo, refShotInfo);
        if (refObj == null)
            return;

        ApplyGrantedActions(refObj);
        OnBulletFired();
    }

    private float RollSpeed()
    {
        return UnityEngine.Random.Range(m_SOAttackInfo.Speed - m_SOAttackInfo.SpeedOffset, m_SOAttackInfo.Speed + m_SOAttackInfo.SpeedOffset);
    }

    private Quaternion ApplyInaccuracy(Quaternion _qBase)
    {
        if (m_fInaccuracyAngle <= 0f)
            return _qBase;

        Quaternion qJitter = Quaternion.Euler(
            UnityEngine.Random.Range(-m_fInaccuracyAngle, m_fInaccuracyAngle),
            UnityEngine.Random.Range(-m_fInaccuracyAngle, m_fInaccuracyAngle),
            0f);

        return qJitter * _qBase;
    }


    private void OnBulletFired()
    {
        if (m_refEffectObject != null)
            m_refEffectObject.Play();

        m_fLastFireTime = Time.time;
        m_fFireTime = m_refAttackInfo.CoolDown;
    }


    public bool CheckTime()
    {
        return (Time.time - m_fLastFireTime) > m_fFireTime;
    }

    // 기존 배율에 누적 곱하지 않고 매번 기본 쿨다운 기준으로 재계산 (Repeatable 기능 재적용 시 드리프트 방지)
    public void SetCooldownMultiplier(float _fMultiplier)
    {
        float fClamped = Mathf.Max(_fMultiplier, 0.1f);

        m_refAttackInfo.CoolDown = m_fBaseCooldown * fClamped;
        m_fFireTime = m_refAttackInfo.CoolDown;
    }

    // FeatureSO.Apply()에서 명중 시 발동 능력(예: SOFeatureHitCreateBullet)이 호출.
    // 레벨업 시점(드물게 발생)에만 배열을 늘리므로 Array.Resize의 GC Alloc은 문제 없음
    public void AddArriveAction(SOBulletAction _refAction)
    {
        AddGrantedAction(ref m_arrArriveActions, _refAction);
    }

    public void AddHitAction(SOBulletAction _refAction)
    {
        AddGrantedAction(ref m_arrHitActions, _refAction);
    }

    private void AddGrantedAction(ref SOBulletAction[] _arrActions, SOBulletAction _refAction)
    {
        int iOldLen = _arrActions?.Length ?? 0;
        Array.Resize(ref _arrActions, iOldLen + 1);
        _arrActions[iOldLen] = _refAction;
    }

    // Pool 재사용 총알이라 매번 덮어써야 중복 실행을 막을 수 있음.
    // Hit은 IAttackObject 공통 계약이라 Bullet/Laser 모두 적용, Arrive는 Laser에 없는 개념이라 Bullet에만 적용
    private void ApplyGrantedActions(GameObject _refBulletObj)
    {
        if (m_arrArriveActions == null && m_arrHitActions == null)
            return;

        IAttackObject refAttackObj = _refBulletObj.GetComponent<IAttackObject>();
        if (refAttackObj == null)
            return;

        if (m_arrHitActions != null)
            refAttackObj.SetWeaponHitActions(m_arrHitActions);

        if (m_arrArriveActions != null && refAttackObj is Bullet refBullet)
            refBullet.SetWeaponArriveActions(m_arrArriveActions);
    }
}

