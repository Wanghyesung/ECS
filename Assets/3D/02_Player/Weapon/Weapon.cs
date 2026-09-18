using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                    Weapon
목적 : 공격 데이터를 소유하고 새 탄환 또는 준비된 탄환을 같은 경로로 발사한다.
 *///////////////////////////////////////////
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

    public int SetMaxAttackCount
    {
        get{return m_refAttackInfo.MaxHitCount;}
        set{m_refAttackInfo.MaxHitCount = value;}
    }

    [SerializeField] private Transform m_refFireTr = null;
    public Transform FireTransform => m_refFireTr;
    [SerializeField] private ParticleSystem m_refEffectObject;

    private float m_fFireTime = 0.2f;
    private float m_fBaseCooldown = 0.2f;
    private float m_fLastFireTime = -Mathf.Infinity;

    // SO 원본 데미지. 공격력 스탯은 여기에 배율로 얹으므로, 누적 곱 대신 매번 base 기준으로 재계산한다
    // (SetCooldown이 m_fBaseCooldown을 쓰는 것과 같은 이유 - Repeatable 기능 재적용 시 드리프트 방지)
    private int m_iBaseDamage;
    private float m_fAttackBonusRate;

    private eWeaponType m_eWeapoonType = eWeaponType.None;
    public eWeaponType WeaponType => m_eWeapoonType;

    public SOPoolData FireBulletPrefab => m_SOAttackInfo.PoolPrefab;

    [Header("Weapon Option")]
    [SerializeField] private bool m_bLookTarget = true;

    // 자동사격에서 제외하되 Player.m_listWeapon의 카드 강화는 그대로 받는다.
    [SerializeField] private bool m_bChargeOnly = false;
    public bool ChargeOnly => m_bChargeOnly;

    public void SetChargeOnly(bool _bChargeOnly) => m_bChargeOnly = _bChargeOnly;

    [Header("Inaccuracy")]
    [SerializeField] private float m_fInaccuracyAngle = 0f; // 조준 방향에서 좌우/상하로 흔들리는 오차 각도

    [Header("Circular Sector Shot")]
    [SerializeField] private int m_iBulletCount = 1; // 1이면 기존처럼 단발
    [SerializeField] private float m_fSpreadAngle = 30f; // 부채꼴(원뿔) 전체 각도

    private const float GOLDEN_ANGLE_DEG = 137.50776f;

    // 레벨업 등으로 획득한 동적 능력치. 총알 프리팹 개수와 무관하게 Weapon(적은 개수) 쪽에 모아두고
    // 발사할 때마다 총알 인스턴스에 참조로 얹어줌 (AddArriveAction/AddHitAction 참고)
    private List<SOBulletAction> m_listArriveActions;
    private List<SOBulletAction> m_listHitActions;

    public void Init()
    {
        // Awake(Monster/Drone) 또는 런 시작(Player.ResetRun)에서 호출. 이 무기가 쏜 총알은 전부 이 참조를 공유하므로
        // 이미 발사한 무기에 다시 부르는 건 런 시작(풀이 씬과 함께 재생성돼 이전 총알이 없는 시점)에서만 — 런 도중 재호출 금지
        m_refAttackInfo = m_SOAttackInfo.MakeAttackInfo();
        m_refAttackInfo.Owner = gameObject.transform;
        m_eWeapoonType = m_SOAttackInfo.WeaponType;
        m_fBaseCooldown = m_refAttackInfo.CoolDown;
        m_iBaseDamage = m_refAttackInfo.Damage;

        // 런 리셋: Start는 1회뿐이라 발사 주기도 여기서. 카드 누적분(공격 배율·명중/도착 액션)은 새 AttackInfo에 없으니 같이 비운다
        m_fFireTime = m_refAttackInfo.CoolDown;
        m_fAttackBonusRate = 0.0f;
        if (m_listArriveActions != null) m_listArriveActions.Clear();
        if (m_listHitActions != null) m_listHitActions.Clear();
    }

    private void Start()
    {
        if (m_refAttackInfo == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"[{name}] 공격 SO 에셋설정을 안 함", this);
            //UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return;
        }

        m_fFireTime = m_refAttackInfo.CoolDown;
        m_fLastFireTime = Time.time;
    }

    public GameObject Fire(Vector3 _vTargetPos, Transform _refTargetTr)
    {
        tShotInfo refShotInfo = new tShotInfo();
        refShotInfo.TargetPos = _vTargetPos;
        refShotInfo.TargetTr = _refTargetTr;
        refShotInfo.Speed = RollSpeed();

        if (m_iBulletCount > 1)
        {
            FireCircularSector(_vTargetPos, refShotInfo);
            return null;  // 다수 펠릿은 준비 탄환 하나를 반환할 수 없어 차지샷 대상에서 제외
        }

        GameObject refObj = ObjectPoolManager.m_Instance.GetObject(m_SOAttackInfo.PoolPrefab);
        if (refObj == null)
            return null;

        FirePrepared(refObj, refShotInfo);
        return refObj;
    }

    // SetAttack을 호출하지 않고 준비 상태로 인출한다. 풀 수명·이동·판정은 발사 때 시작한다.
    public GameObject Begin()
    {
        if (m_refAttackInfo == null || m_refFireTr == null || m_iBulletCount != 1)
            return null;
        if (ObjectPoolManager.m_Instance == null)
            return null;

        PoolObject refPrefab = ObjectPoolManager.m_Instance.GetPoolPrefab(FireBulletPrefab);
        if (refPrefab == null)
            return null;

        GameObject refObj = ObjectPoolManager.m_Instance.GetObject(FireBulletPrefab);
        if (refObj == null)
            return null;

        Bullet refBullet = refObj.GetComponent<Bullet>();
        CircleCollider refCollider = refObj.GetComponent<CircleCollider>();
        PoolObject refPoolObj = refObj.GetComponent<PoolObject>();
        if (refBullet == null || refCollider == null || refPoolObj == null)
        {
            ObjectPoolManager.m_Instance.PushObject(refObj);
            return null;
        }

        refBullet.enabled = false;
        refCollider.enabled = false;
        refPoolObj.SuspendLifetime();
        return refObj;
    }

    public void Fire(GameObject _refPreparedObj, Vector3 _vTargetPos,
        Transform _refTargetTr, float _fSpeedMultiplier)
    {
        var tShotInfo = new tShotInfo
        {
            TargetPos = _vTargetPos,
            TargetTr = _refTargetTr,
            Speed = Mathf.Max(0.01f, RollSpeed()) * _fSpeedMultiplier
        };
        FirePrepared(_refPreparedObj, tShotInfo);
    }

    private void FirePrepared(GameObject _refObj, tShotInfo _tShotInfo)
    {
        Vector3 vLookDir = _tShotInfo.TargetPos - m_refFireTr.position;
        Quaternion qRot = m_bLookTarget && vLookDir.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(vLookDir) : m_refFireTr.rotation;
        _refObj.transform.SetPositionAndRotation(m_refFireTr.position, ApplyInaccuracy(qRot));

        Bullet refBullet = _refObj.GetComponent<Bullet>();
        if (refBullet != null)
            refBullet.enabled = true;

        IAttackObject refAttackObj = _refObj.GetComponent<IAttackObject>();
        if (refAttackObj == null)
        {
            ObjectPoolManager.m_Instance.PushObject(_refObj);
            return;
        }

        ApplyActions(_refObj);
        refAttackObj.SetAttack(m_refAttackInfo, _tShotInfo);
        if (refBullet != null && _refObj.TryGetComponent(out CircleCollider refCollider))
            refCollider.enabled = true;
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

            if (vDir.sqrMagnitude < 0.0001f)
                vDir = vBaseDir;

            Quaternion qRot = ApplyInaccuracy(Quaternion.LookRotation(vDir));

            tShotInfo refPelletShotInfo = _refShotInfo;
            refPelletShotInfo.Speed = RollSpeed();

            GameObject refObj = Bullet.SpawnAttackObject(m_SOAttackInfo.PoolPrefab, m_refFireTr.position, qRot, m_refAttackInfo, refPelletShotInfo);
            if (refObj == null)
                continue;

            ApplyActions(refObj);
            OnBulletFired();
        }
    }


    public void FireAndRotate(Vector3 _vDir, float _fFowardOffset)
    {
        if (_vDir.sqrMagnitude < 0.0001f)
            _vDir = m_refFireTr.forward;

        Vector3 vSpawnPos = m_refFireTr.position + (_vDir * _fFowardOffset);
        Quaternion qRot = ApplyInaccuracy(Quaternion.LookRotation(_vDir));

        tShotInfo refShotInfo = new tShotInfo();
        refShotInfo.Speed = RollSpeed();

        GameObject refObj = Bullet.SpawnAttackObject(m_SOAttackInfo.PoolPrefab, vSpawnPos, qRot, m_refAttackInfo, refShotInfo);
        if (refObj == null)
            return;

        ApplyActions(refObj);
        OnBulletFired();
    }

    private float RollSpeed()
    {
        float fSpeed = m_refAttackInfo.Speed;
        return UnityEngine.Random.Range(fSpeed - m_SOAttackInfo.SpeedOffset, fSpeed + m_SOAttackInfo.SpeedOffset);
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
    public void SetCooldown(float _fValue)
    {
        float fClamped = Mathf.Max(_fValue, 0.1f);

        m_refAttackInfo.CoolDown = m_fBaseCooldown * fClamped;
        m_fFireTime = m_refAttackInfo.CoolDown;
    }

    // Player.AddAttack()에서 호출. m_refAttackInfo는 이 무기가 만든 모든 총알이 참조하는 인스턴스라
    // 여기만 고치면 이미 날아가는 총알을 뺀 다음 발사분부터 전부 반영된다.
    // 평탄 가산이 아니라 '% 증가'인 이유 : 한 번에 여러 발 나가는 무기(m_iBulletCount)에 평탄 가산을 하면
    // 실제 증가폭이 탄 개수에 비례해 터진다 (샷건 16발 = 가산치의 16배). 배율이면 탄 개수와 무관하게 같은 비율로 오른다
    public void AddAttackRate(float _fRate)
    {
        m_fAttackBonusRate += _fRate;
        m_refAttackInfo.Damage = Mathf.Max(1, Mathf.RoundToInt(m_iBaseDamage * (1f + (m_fAttackBonusRate * 0.01f))));
    }

    public void AddBulletSpeed(float _fValue)
    {
        m_refAttackInfo.Speed += _fValue;
    }
    public void DownBulletSpeed(float _fValue)
    {
        m_refAttackInfo.Speed -= _fValue;
    }

    // FeatureSO.Apply()에서 명중 시 발동 능력(예: SOFeatureHitCreateBullet)이 호출.

    public void AddArriveAction(SOBulletAction _refAction)
    {
        AddAction(ref m_listArriveActions, _refAction);
    }
    public void CancelArriveAction(SOBulletAction _refAction)
    {
        CancelAction(ref m_listArriveActions, _refAction);
    }


    public void AddHitAction(SOBulletAction _refAction)
    {
        AddAction(ref m_listHitActions, _refAction);
    }

    public void CancelHitAction(SOBulletAction _refAction)
    {
        CancelAction(ref m_listHitActions, _refAction);
    }

    private void AddAction(ref List<SOBulletAction> _listActions, SOBulletAction _refAction)
    {
        if (_listActions == null)
            _listActions = new List<SOBulletAction>();
        _listActions.Add(_refAction);
    }

    private void CancelAction(ref List<SOBulletAction> _listActions, SOBulletAction _refAction)
    {
        _listActions?.Remove(_refAction);
    }

    // Pool 재사용 총알이라 매번 덮어써야 중복 실행을 막을 수 있음.
    // Hit은 IAttackObject 공통 계약이라 Bullet/Laser 모두 적용, Arrive는 Laser에 없는 개념이라 Bullet에만 적용
    private void ApplyActions(GameObject _refBulletObj)
    {
        IAttackObject refAttackObj = _refBulletObj.GetComponent<IAttackObject>();
        if (refAttackObj == null)
            return;

        // 같은 풀을 여러 무기가 사용하므로 액션이 없는 무기도 이전 발사분을 지운다.
        refAttackObj.SetWeaponHitActions(m_listHitActions);

        if (refAttackObj is Bullet refBullet)
            refBullet.SetWeaponArriveActions(m_listArriveActions);
    }
}

