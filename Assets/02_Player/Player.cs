using System;
using System.Collections;
using System.Collections.Generic;
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

    public long CurrentHP;

    public float Speed;

    public ushort CurrentEffects; 
    public EffectEntry[] Effects = new EffectEntry[(int)eStatusEffect.End];

}

public class Player : MonoBehaviour, IDamageable, IChangeInfoable
{
    [SerializeField] private List<Weapon> m_listWeapon = null;

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
    public ObjectInfo PlayerInfo => m_refObjectInfo;

    [SerializeField] private SOObjectInfo m_SOObjectInfo = null;

    [SerializeField] private SliderImage m_refHPSliderImage = null;
    [SerializeField] private SliderImage m_refExSliderImage = null;

    [SerializeField] private TargetScanner m_refTargetScnner = null;

    private Coroutine m_CoNockback = null;
    private Rigidbody m_refRigidbody = null;

    private void Awake()
    {
        m_refRigidbody = GetComponent<Rigidbody>();
        m_refMovement = GetComponent<PlayerMovement>();
    }

    private void Start()
    {
        m_refObjectInfo.CurrentHP = m_SOObjectInfo.MaxHP;

        m_refHPSliderImage.OnFillCompleted += Dead;
        m_refHPSliderImage.SetRange(m_SOObjectInfo.MaxHP, m_refObjectInfo.CurrentHP);

        // EXP는 Player 소유가 아닌 BattleManager 소유 지표라 이벤트 구독으로만 UI 갱신

        BattleManager.m_Instance.OnExpChanged += HandleExpChanged;
        m_refExSliderImage.SetRange(BattleManager.m_Instance.MaxExp, BattleManager.m_Instance.CurrentExp);

        // ExSlider가 실제로 Max까지 다 찬 시점에 레벨업(카드 UI)을 확정 (몬스터 사망 즉시가 아님)
        m_refExSliderImage.OnFillMaxReached += MapExpSlider;

        m_fLastRollTime = Time.time;
    }

    private void OnDestroy()
    {
        m_refHPSliderImage.OnFillCompleted -= Dead;
        m_refExSliderImage.OnFillMaxReached -= MapExpSlider;

        if (BattleManager.m_Instance != null)
            BattleManager.m_Instance.OnExpChanged -= HandleExpChanged;
    }

    private void HandleExpChanged(int _iCurrentExp, int _iMaxExp)
    {
        m_refExSliderImage.UpdateSlider(_iCurrentExp, _iMaxExp);
    }
    
    private void MapExpSlider()
    {
        BattleManager.m_Instance.LevelUp();
    }

    private void Update()
    {
        tInputInfo tInfo = InputManager.m_Instance.InputInfo;
        bool bOnSpace = tInfo.OnSpace;
        bool bInputX = tInfo.MoveDir.x != 0;

        if (bOnSpace == true && bInputX == true)
            MoveRoll();


        if (Input.GetKey(KeyCode.Q))
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
        float fRollDir = InputManager.m_Instance.InputInfo.MoveDir.x;
        if (Mathf.Approximately(fRollDir, 0.0f) == true)
            fRollDir = 1.0f;

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
        if (m_CoNockback != null)
            StopCoroutine(m_CoNockback);

        m_refObjectInfo.State = eEntityState.Hit;

        m_refObjectInfo.CurrentHP -= _refAttackInfo.Damage;
        m_refHPSliderImage.UpdateSlider(m_refObjectInfo.CurrentHP, m_SOObjectInfo.MaxHP);
        m_CoNockback = StartCoroutine(CoNockback(_refAttackInfo, _refShotInfo));
    }

    private IEnumerator CoNockback(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
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

            yield return null;
        }

        m_CoNockback = null;
        m_refObjectInfo.State = eEntityState.Idle;
    }

    // FeatureSO.Apply()에서 무기 추가
    // 씬에 미리 배치된 비활성 무기를 활성화하는 방식 (런타임 Instantiate 회피)
    public void SetActiveWweapon(eWeaponType _eType)
    {
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].WeaponType == _eType && m_listWeapon[i].gameObject.activeSelf == false)
                m_listWeapon[i].gameObject.SetActive(true);
        }
    }

    // FeatureSO.Apply()에서 공격속도 강화 기능(예: SOFeatureAttackSpeedUp)이 호출
    public void ModifyWeaponCooldown(eWeaponType _eType, float _fMultiplier)
    {
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].WeaponType == _eType)
                m_listWeapon[i].SetCooldownMultiplier(_fMultiplier);
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

    public void ChangeHPRatio(float _fRatio)
    {
        throw new NotImplementedException();
    }

    public void UpHPRatio(float _fRatio)
    {
        float fAccValue = m_SOObjectInfo.MaxHP * _fRatio;
        long lAccValue = (long)fAccValue;

        UpHP(lAccValue);
    }

    public void ChangeSpeedRatio(float _fRatio)
    {
        throw new NotImplementedException();
    }

    public void UpHP(long _lValue)
    {
        m_refObjectInfo.CurrentHP += _lValue;
        if (m_refObjectInfo.CurrentHP >= m_SOObjectInfo.MaxHP)
            m_refObjectInfo.CurrentHP = m_SOObjectInfo.MaxHP;
    }


    // IChangeInfoable 기능 구현



}
