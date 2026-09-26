using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using Unity.AI;
using UnityEngine.AI;
using static Weapon;
using UnityEngine.Events;


public enum eStatusEffect
{
    Wait,
    Lock,
    Stun,
    Poison,
    Burn,
    End,
}

public enum eEntityState
{
    None,
    Idle,
    Move,
    Attack,
    Hit,
    Lock,
    Dead,
    Success,
    Fail,
    End,
}


[Serializable]
public struct EffectEntry
{
    public float EndTime;
    public float TickInterval;
    public float NextTickTime;
    public long  TickDamage;
}

[Serializable]
public class ObjectInfo
{
    public eEntityState State;

    public long MaxHP;
    public readonly ReactiveProperty<long> CurrentHP = new();

    public float Speed;

    public float Attack;
    public float Defense;

    public ushort CurrentEffects;
    public EffectEntry[] Effects = new EffectEntry[(int)eStatusEffect.End];
}

public class Player : MonoBehaviour, IDamageable, IChangeInfoable
{
    [SerializeField] private List<Weapon> m_listWeapon = null;
    [SerializeField] private List<Drone> m_listDrone = null;

    [SerializeField] private AnimationTable m_refAnimTable = null;
    [SerializeField] private VisualObject m_refVisualPlayer = null;
    private PlayerMovement m_refMovement = null;
    private Aim m_refAim = null;
    public Aim Aim => m_refAim;

    [Header("Max Roll")]
    [SerializeField] private float m_fRollTime = 2.0f;
    private float m_fLastRollTime;

    [SerializeField] private float m_fMaxRollDuration = 0.5f;
    [SerializeField] private float m_fMaxRollSpeedBoost = 2.5f; // 부스트 시작 배율
    [SerializeField] private float m_fMaxRollSpeedDecay = 3.0f; // 초당 배율 감소량

    [SerializeField] private ObjectInfo m_refObjectInfo = new ObjectInfo();
    public ObjectInfo ObjectInfo => m_refObjectInfo;

    // 런 시작 시 되돌릴 기본 활성 상태 — 씬에 배치된 그대로(BaseWeapon_0/1만 켜짐)가 곧 기본 로드아웃.
    // 별도 [SerializeField] 없이 Awake 스냅샷으로 충분 (인스펙터에 같은 정보가 두 번 생기는 걸 피함)
    private bool[] m_arrWeaponDefaultActive;
    private bool[] m_arrDroneDefaultActive;

    // 차지 무기는 WeaponCon(PlayerChargeController가 구동)이 붙어 있는 것으로 구분한다 — Weapon의 ChargeOnly 플래그를 대체.
    // Fire()가 Update에서 도는 핫 루프라 TryGetComponent는 Awake에서 한 번만 (performance.md의 캐싱 규칙)
    private bool[] m_arrWeaponChargeDriven;

    [SerializeField] private SOObjectInfo m_SOObjectInfo = null;
    [Header("Audio")]
    [SerializeField] private SOAudio m_SODeadAudio;


    [SerializeField] private TargetScanner m_refTargetScnner = null;


    private CancellationTokenSource m_ctsNockback;
    private Rigidbody m_refRigidbody = null;

    public Rigidbody Rigidbody => m_refRigidbody;

    private static Player ThisPlayer = null;
    public static Player CurrentPlayer {  get { return ThisPlayer; } }

    private static readonly Subject<Unit> m_subjectDied = new();
    public static Observable<Unit> OnPlayerDied => m_subjectDied;   // DungeonManager가 런 종료 처리 (Monster.OnMonsterDied와 동일 구조)

    [SerializeField] private bool TestLock = false;
    private void Awake()
    {
        // 로비는 LoadSceneMode.Single로 매번 다시 로드되므로 씬의 MainPlayer가 런마다 또 Awake 된다.
        // 가드가 없으면 새 인스턴스가 CurrentPlayer를 덮어써 DDOL 원본은 죽은 채로 남고 런마다 Player가 하나씩 는다 (검증 중 실제 발생).
        // Destroy는 프레임 끝이라 그 전에 자식 Weapon.Start가 돌면 Init 안 된 AttackInfo로 빌드에선 Application.Quit — 먼저 꺼서 막는다
        if (ThisPlayer != null && ThisPlayer != this) { gameObject.SetActive(false); Destroy(gameObject); return; }

        m_refRigidbody = GetComponent<Rigidbody>();
        m_refMovement = GetComponent<PlayerMovement>();
        m_refAim = GetComponent<Aim>();

        ThisPlayer = this;

        m_arrWeaponDefaultActive = new bool[m_listWeapon.Count];
        m_arrWeaponChargeDriven = new bool[m_listWeapon.Count];
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            m_arrWeaponDefaultActive[i] = m_listWeapon[i].gameObject.activeSelf;
            m_arrWeaponChargeDriven[i] = m_listWeapon[i].TryGetComponent(out WeaponCon _);
        }
        m_arrDroneDefaultActive = new bool[m_listDrone.Count];
        for (int i = 0; i < m_listDrone.Count; ++i)
            m_arrDroneDefaultActive[i] = m_listDrone[i].gameObject.activeSelf;

        DontDestroyOnLoad(this);
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (ThisPlayer != this)   // Awake 가드로 파괴 예약된 중복 인스턴스도 이 프레임엔 OnEnable이 돈다 — 캐싱 안 된 참조로 ResetRun 하면 NRE
            return;
        ResetRun();
    }

    private void Start()
    {
        m_refObjectInfo.CurrentHP.Where(_lHp => _lHp <= 0).Subscribe(_ => Dead()).AddTo(this);   // 사망 판정은 UI가 아니라 Player 자신이

        InputManager.m_Instance.OnMoveButtonPressed.Subscribe(_ => MoveRoll()).AddTo(this);

        m_fLastRollTime = Time.time;
    }

    private void ResetRun()
    {
        CancelNockback();

        // 1) 엔티티 스탯 = SO 기본값. Attack/Defense/Speed는 직렬화 0에서 시작하는 '보너스'라 0으로
        m_refObjectInfo.MaxHP = m_SOObjectInfo.MaxHP;
        m_refObjectInfo.Attack = 0.0f;
        m_refObjectInfo.Defense = 0.0f;
        m_refObjectInfo.Speed = 0.0f;
        m_refObjectInfo.CurrentEffects = 0;
        Array.Clear(m_refObjectInfo.Effects, 0, m_refObjectInfo.Effects.Length);
        m_refMovement.ResetMoveSpeed();

        // 2) 무기/드론 = 씬 배치 상태로. Weapon.Init이 AttackInfo를 SO에서 새로 만들어
        //    카드로 올린 Damage/CoolDown/Speed/MaxHitCount와 명중·도착 액션이 같이 사라진다
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            m_listWeapon[i].Init();
            m_listWeapon[i].gameObject.SetActive(m_arrWeaponDefaultActive[i]);
        }
        for (int i = 0; i < m_listDrone.Count; ++i)
            m_listDrone[i].gameObject.SetActive(m_arrDroneDefaultActive[i]);

        // 3) 영구 성장(로비 강화·장비)만 다시 얹는다 — 기본값 위에 리스트 전체를 적용하므로 런을 거듭해도 중복 누적 없음
        PlayerPreLoadData.ApplyTo(this);

        // 4) 장비 HP 보너스까지 포함해 만땅으로 시작 (기존엔 SO값으로 먼저 채워 100/120 상태로 시작했음)
        m_refObjectInfo.State = eEntityState.Idle;
        m_refObjectInfo.CurrentHP.Value = m_refObjectInfo.MaxHP;
        m_refMovement.enabled = true;
    }

    private void Update()
    {
        if (TestLock == true || m_refObjectInfo.State == eEntityState.Dead) return;

        Fire();
    }

    private void OnTriggerEnter(Collider other)
    {
        
    }


    public void UpdateOnAnimation(eEntityState _eState, bool _bOn)
    {
        m_refAnimTable.SetBool(_eState, _bOn);
    }

    public void UpdateOnTriggerAnimation(eEntityState _eState)
    {
        m_refAnimTable.SetTrigger(_eState);
    }

    // 스페이스 입력 시 z축 기준 360도 배럴롤 연출과 함께 이동속도를 순간적으로 올렸다가 서서히 되돌림
    private void MoveRoll()
    {
        float fCurTime = Time.time - m_fLastRollTime;
        if (fCurTime < m_fRollTime)
            return;

        m_fLastRollTime = Time.time;
        // 대각선 입력도 좌/우 성분으로 자연스럽게 투영되도록 조준 방향(transform.right) 기준 내적으로 부호 판정
        Vector2 vMoveInput = InputManager.m_Instance.InputInfo.MoveDir;
        Vector3 vKeyDir = new Vector3(vMoveInput.x, 0.0f, vMoveInput.y);
        float fRollDir = vKeyDir.x > 0 ? 1.0f : -1.0f;
        
        m_refVisualPlayer.PlayMaxRoll(fRollDir, m_fMaxRollDuration);
        m_refMovement.ApplySpeedBoost(fRollDir, m_fMaxRollSpeedBoost, m_fMaxRollSpeedDecay);
    }

    private void Fire()
    {
        Vector3 vTargetPos = m_refAim.TargetPosition;
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].gameObject.activeSelf == false)
                continue;

            if (m_arrWeaponChargeDriven[i] == true)   // 차지 무기는 좌클릭 릴리즈(PlayerChargeController)가 발사
                continue;

            if (m_listWeapon[i].CheckTime() == true)
                m_listWeapon[i].Fire(vTargetPos, m_refTargetScnner.Target);
        }
    }


    private void Dead()
    {
        CancelNockback();
        m_refObjectInfo.State = eEntityState.Dead;   // Update의 Fire 차단
        SoundManager.m_Instance.PlaySfx(m_SODeadAudio);
        m_refMovement.enabled = false;               // PlayerMovement는 상태를 안 보므로 컴포넌트째 끔 (OnEnable에서 복구)
        m_subjectDied.OnNext(Unit.Default);
    }

    public void TakeDamage(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        if (m_refObjectInfo.State == eEntityState.Dead)   // Monster.TakeDamage와 같은 가드 — 중복 사망 방지
            return;

        CancelNockback();

        m_refObjectInfo.State = eEntityState.Hit;

        int iFinalDamage = (int)Mathf.Max(_refAttackInfo.Damage - m_refObjectInfo.Defense, 0f);
        if (iFinalDamage > 0)
            SoundManager.m_Instance.PlaySfx(_refAttackInfo.HitAudio, transform.position);
        m_refObjectInfo.CurrentHP.Value -= iFinalDamage;
        if (m_refObjectInfo.State == eEntityState.Dead)   // 위 대입에서 Dead()가 동기 호출됨 — 넉백을 시작하면 끝에서 State=Idle로 되살아난다
            return;

        m_ctsNockback = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        NockbackAsync(_refAttackInfo, _refShotInfo, m_ctsNockback.Token).Forget();
    }

    private void CancelNockback()
    {
        if (m_ctsNockback == null) 
            return;
        m_ctsNockback.Cancel();
        m_ctsNockback.Dispose();
        m_ctsNockback = null;
    }

    private async UniTaskVoid NockbackAsync(AttackInfo _refAttackInfo, tShotInfo _refShotInfo, CancellationToken _ct)
    {
        Vector3 vDir = _refShotInfo.MoveDir;
        float fDuration = Mathf.Max(_refAttackInfo.KnockbackDuration, 0.0001f);
        float fPower = _refAttackInfo.AttackPower;

        float fMagnitude = Mathf.Clamp(fPower / 50.0f, 0.0f, 2.0f);

        if (m_refVisualPlayer != null)
        {
            CameraManager.m_Instance.StartShakeCamera(fMagnitude);
            m_refVisualPlayer.PlayHitShake(vDir, fPower);
        }

        float fNockPower = _refAttackInfo.KnockbackForce;
        float fElapsed = 0f;
        while (fElapsed < fDuration)
        {
            float fRevElaps = 1.0f - (fElapsed / fDuration);
            Vector3 vDelta = vDir * fNockPower * fRevElaps * Time.deltaTime;

            m_refRigidbody.MovePosition(m_refRigidbody.position + vDelta);

            fElapsed += Time.deltaTime;

            await UniTask.Yield(_ct);
        }

        m_ctsNockback.Dispose();
        m_ctsNockback = null;
        m_refObjectInfo.State = eEntityState.Idle;
    }

    // FeatureSO.Apply()에서 무기 추가
    // 씬에 미리 배치된 비활성 무기를 활성화하는 방식 (런타임 Instantiate 회피)
    public void ActiveWweapon(eWeaponType _eType)
    {
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].WeaponType == _eType && m_listWeapon[i].gameObject.activeSelf == false)
            {
                m_listWeapon[i].gameObject.SetActive(true);
                return;
            }
        }
    }

    public void ActivDrone()
    {
        for (int i = 0; i < m_listDrone.Count; ++i)
        {
            if (m_listDrone[i].gameObject.activeSelf == false)
            {
                m_listDrone[i].gameObject.SetActive(true);
                return;
            }
        }
    }



    public void UnActiveWweapon(eWeaponType _eType)
    {
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].WeaponType == _eType && m_listWeapon[i].gameObject.activeSelf == true)
            {
                m_listWeapon[i].gameObject.SetActive(false);
                return;
            }
        }
    }

    public void UnActivDrone()
    {
        for (int i = 0; i < m_listDrone.Count; ++i)
        {
            if (m_listDrone[i].gameObject.activeSelf == true)
            {
                m_listDrone[i].gameObject.SetActive(false);
                return;
            }
        }
    }



    // FeatureSO.Apply()에서 공격속도 강화 기능(예: SOFeatureAttackSpeedUp)이 호출
    public void SetWeaponCooldown(eWeaponType _eType, float _fValue)
    {
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].WeaponType == _eType)
                m_listWeapon[i].SetCooldown(_fValue);
        }
    }

    // FeatureSO.Apply()에서 명중 시 발동 능력(예: SOFeatureHitCreateBullet)이 호출
    public void AddWeaponHitAction(eWeaponType _eType, SOBulletAction _refAction)
    {
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].WeaponType == _eType)
                m_listWeapon[i].AddHitAction(_refAction);
        }
    }

    // FeatureSO.Apply()에서 총알이 사라질 시 발동 능력(예: SOFeatureHitCreateBullet)이 호출
    public void AddWeaponArriveAction(eWeaponType _eType, SOBulletAction _refAction)
    {
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].WeaponType == _eType)
                m_listWeapon[i].AddArriveAction(_refAction);
        }
    }

    public void CancelWeaponArriveAction(eWeaponType _eType, SOBulletAction _refAction)
    {
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].WeaponType == _eType)
                m_listWeapon[i].CancelArriveAction(_refAction);
        }
    }

    public void CancelWeaponHitAction(eWeaponType _eType, SOBulletAction _refAction)
    {
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].WeaponType == _eType)
                m_listWeapon[i].CancelHitAction(_refAction);
        }
    }

    // FeatureSO.Apply()에서 공격 카운트를 높여주는 기능 
    public void PenetrationWeapon(eWeaponType _eType, int _iValue)
    {
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].WeaponType == _eType)
                m_listWeapon[i].SetMaxAttackCount = _iValue;
        }
    }



    // IChangeInfoable 기능 구현
    public void ChangeSpeedRatio(float _fRatio)
    {
        throw new NotImplementedException();
    }


    public void ChangeHPRatio(float _fRatio)
    {
        throw new NotImplementedException();
    }


    public void UpHPRatio(float _fRatio)
    {
        float fAccValue = m_SOObjectInfo.MaxHP * _fRatio;
        long lAccValue = (long)fAccValue;

        AddHP(lAccValue);
    }

    public void DownHPRatio(float _fRatio)
    {
        float fAccValue = m_SOObjectInfo.MaxHP * _fRatio;
        long lAccValue = (long)fAccValue;

        AddHP(-lAccValue);
    }


    public void AddHP(long _lValue)
    {
        m_refObjectInfo.CurrentHP.Value = System.Math.Min(m_refObjectInfo.CurrentHP.Value + _lValue, m_refObjectInfo.MaxHP);
    }

    // 장비 등으로 얻는 HP 증가분은 무기별 상한이 없는 BulletSpeed와 동일하게 상한 없이 누적.
    // (SOObjectInfo.MaxHP는 시작값일 뿐 상한이 아님 - 여기서 다시 클램프하면 증가분이 항상 무효화됨)
    public void AddMaxHP(long _lValue)
    {
        m_refObjectInfo.MaxHP += _lValue;
    }

    // 공격력 스탯은 무기 데미지에 얹는 '증가율(%)'이고, MaxAtack이 그 상한이다 (기준이 MaxHP였던 건 오타)
    public void UpAttackRatio(float _fRatio)
    {
        float fAccValue = m_SOObjectInfo.MaxAtack * _fRatio;
        int iAccValue = (int)fAccValue;

        AddAttack(iAccValue);
    }

    public void DownAttackRatio(float _fRatio)
    {
        float fAccValue = m_SOObjectInfo.MaxAtack * _fRatio;
        int iAccValue = (int)fAccValue;

        AddAttack(-iAccValue);
    }

    public void AddAttack(int _iValue)
    {
        float fPrevAttack = m_refObjectInfo.Attack;

        m_refObjectInfo.Attack = Mathf.Clamp(m_refObjectInfo.Attack + _iValue, 0.0f, m_SOObjectInfo.MaxAtack);

        // MaxAtack 클램프로 실제 증가분이 _iValue보다 작을 수 있어 그 차이만 무기에 반영.
        float fAppliedValue = m_refObjectInfo.Attack - fPrevAttack;
        if (fAppliedValue == 0.0f)
            return;

        // 비활성(미해금) 무기까지 전부 반영한다 - 활성 무기만 갱신하면 공격력 카드를 먼저 먹고
        // 나중에 해금한 무기가 그때까지 쌓인 보너스를 영영 못 받는다
        for (int i = 0; i < m_listWeapon.Count; ++i)
            m_listWeapon[i].AddAttackRate(fAppliedValue);
    }

    public void UpSpeedRatio(float _fRatio)
    {
        float fAccValue = m_SOObjectInfo.MaxSpeed * _fRatio;
        AddSpeed(fAccValue);
    }
    public void DownSpeedRatio(float _fRatio)
    {
        float fAccValue = m_SOObjectInfo.MaxSpeed * _fRatio;
        AddSpeed(-fAccValue);
    }

    public void AddSpeed(float _fValue)
    {
        float fPrevSpeed = m_refObjectInfo.Speed;

        m_refObjectInfo.Speed = Mathf.Clamp(m_refObjectInfo.Speed + _fValue, 0.0f, m_SOObjectInfo.MaxSpeed);

        // MaxSpeed 클램프로 실제 증가분이 _fValue보다 작을 수 있어 그 차이만 PlayerMovement에 반영.
        float fAppliedValue = m_refObjectInfo.Speed - fPrevSpeed;
        m_refMovement.AddMoveSpeed(fAppliedValue);
    }

    public void UpDefenseRatio(float _fRatio)
    {
        float fAccValue = m_SOObjectInfo.MaxDefense * _fRatio;
        AddDefense(fAccValue);
    }

    public void DownDefenseRatio(float _fRatio)
    {
        float fAccValue = m_SOObjectInfo.MaxDefense * _fRatio;
        AddDefense(-fAccValue);
    }

    public void AddDefense(float _fValue)
    {
        // 하한 0 - 조커 실패로 방어 카드가 몰수될 때 음수가 되면 피해가 오히려 늘어난다
        m_refObjectInfo.Defense = Mathf.Clamp(m_refObjectInfo.Defense + _fValue, 0.0f, m_SOObjectInfo.MaxDefense);
    }

    // FeatureSO.Apply()에서 총알 속도 강화 기능(예: SOFeatureUpBulletSpeed)이 호출.
    // 무기별 상한이 없어 Attack/Speed처럼 클램프하지 않고 그대로 누적
    public void UpBulletSpeed(float _fValue)
    {
        // AddAttack과 같은 이유로 비활성 무기까지 반영 - 나중에 해금해도 누적분을 그대로 받는다
        for (int i = 0; i < m_listWeapon.Count; ++i)
            m_listWeapon[i].AddBulletSpeed(_fValue);
    }

    public void DownBulletSpeed(float _fValue)
    {
        for (int i = 0; i < m_listWeapon.Count; ++i)
            m_listWeapon[i].DownBulletSpeed(_fValue);
    }

    // FeatureSO.Apply()에서 발당 탄수를 늘리는 기능(SOFeatureAddBulletCount)이 호출.
    // AddAttack과 같은 이유로 비활성 무기까지 반영. 차지 무기는 제외 - Weapon.Begin()이 탄수≠1이면 null을 돌려줘 차지샷이 죽는다
    public void AddWeaponBulletCount(eWeaponType _eType, int _iValue)
    {
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].WeaponType == _eType && m_listWeapon[i].gameObject.activeSelf == true)
                m_listWeapon[i].AddBulletCount(_iValue);
        }
    }
}
