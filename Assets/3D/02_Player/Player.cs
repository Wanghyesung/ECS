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
    [SerializeField] private Aim m_refAim= null;
    [SerializeField] private VisualObject m_refVisualPlayer = null;
    private PlayerMovement m_refMovement = null;

    [Header("Max Roll")]
    [SerializeField] private float m_fRollTime = 2.0f;
    private float m_fLastRollTime;

    [SerializeField] private float m_fMaxRollDuration = 0.5f;
    [SerializeField] private float m_fMaxRollSpeedBoost = 2.5f; // 부스트 시작 배율
    [SerializeField] private float m_fMaxRollSpeedDecay = 3.0f; // 초당 배율 감소량

    [SerializeField] private ObjectInfo m_refObjectInfo = new ObjectInfo();
    public ObjectInfo ObjectInfo => m_refObjectInfo;

    [SerializeField] private SOObjectInfo m_SOObjectInfo = null;

    [SerializeField] private SliderImage m_refHPSliderImage = null;
    [SerializeField] private SliderImage m_refExSliderImage = null;

    [SerializeField] private TargetScanner m_refTargetScnner = null;


    private CancellationTokenSource m_ctsNockback;
    private Rigidbody m_refRigidbody = null;

    private static Player ThisPlayer = null;
    public static Player CurrentPlayer {  get { return ThisPlayer; } }

    [SerializeField] private bool TestLock = false;
    private void Awake()
    {
        m_refRigidbody = GetComponent<Rigidbody>();
        m_refMovement = GetComponent<PlayerMovement>();

        ThisPlayer = this;
        for (int i = 0; i < m_listWeapon.Count; ++i)
            m_listWeapon[i].Init();
    }

    private void Start()
    {
        m_refObjectInfo.CurrentHP.Value = m_SOObjectInfo.MaxHP;
        m_refObjectInfo.MaxHP = m_SOObjectInfo.MaxHP;
        PlayerPreLoadData.ApplyTo(this);

        m_refHPSliderImage.SetRange(m_refObjectInfo.MaxHP, m_refObjectInfo.CurrentHP.Value);
        m_refObjectInfo.CurrentHP.Subscribe(_lHp => m_refHPSliderImage.UpdateSlider(_lHp, m_refObjectInfo.MaxHP)).AddTo(this);
        m_refHPSliderImage.OnFillCompleted.Subscribe(_ => Dead()).AddTo(this);

        // EXP는 BattleManager 소유 지표 — 구독으로만 UI 갱신
        BattleManager refBattle = BattleManager.m_Instance;
        m_refExSliderImage.SetRange(refBattle.MaxExp, refBattle.Exp.CurrentValue);
        refBattle.Exp.Subscribe(_iExp => m_refExSliderImage.UpdateSlider(_iExp, refBattle.MaxExp)).AddTo(this);

        // ExSlider가 실제로 Max까지 다 찬 시점에 레벨업(카드 UI)을 확정
        m_refExSliderImage.OnFillMaxReached.Subscribe(_ => refBattle.LevelUp()).AddTo(this);

        InputManager.m_Instance.OnMoveButtonPressed.Subscribe(_ => MoveRoll()).AddTo(this);

        m_fLastRollTime = Time.time;
    }

    private void Update()
    {
        if (TestLock == true) return;

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

            if (m_listWeapon[i].CheckTime() == true)
                m_listWeapon[i].Fire(vTargetPos, m_refTargetScnner.Target);
        }
    }


    private void Dead()
    {
        //플레이어가 죽었을 때
    }

    public void TakeDamage(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        CancelNockback();

        m_refObjectInfo.State = eEntityState.Hit;

        int iFinalDamage = (int)Mathf.Max(_refAttackInfo.Damage - m_refObjectInfo.Defense, 0f);
        m_refObjectInfo.CurrentHP.Value -= iFinalDamage;
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
}
