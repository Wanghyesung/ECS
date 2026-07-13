using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI;
using UnityEngine.AI;
using static Weapon;


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
    private List<Weapon> m_listFireWeapon = null; 

    [SerializeField] private AnimationTable m_refAnimTable = null;
    [SerializeField] private Aim m_refAim= null;
    [SerializeField] private VisualObject m_refVisualPlayer = null;

    [SerializeField] private ObjectInfo m_refObjectInfo = new ObjectInfo();
    public ObjectInfo PlayerInfo => m_refObjectInfo;

    [SerializeField] private SOObjectInfo m_SOObjectInfo = null;

    [SerializeField] private SliderImage m_refHPSliderImage = null;
    [SerializeField] private SliderImage m_refExSliderImage = null;

    [SerializeField] LayerMask m_tAttackLayer;
    private Transform m_refNearTargetTr = null; 
    private Collider[] m_arrNearCollider = new Collider[20];
    [SerializeField] private float m_fAttackRaius;

    private Coroutine m_CoNockback = null;
    private Rigidbody m_refRigidbody = null;

    private void Awake()
    {
        m_listFireWeapon = new List<Weapon>(m_listWeapon.Count);
        m_refRigidbody = GetComponent<Rigidbody>();
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
        m_refExSliderImage.OnFillMaxReached += HandleExpSliderFilled;
    }

    private void OnDestroy()
    {
        m_refHPSliderImage.OnFillCompleted -= Dead;
        m_refExSliderImage.OnFillMaxReached -= HandleExpSliderFilled;

        if (BattleManager.m_Instance != null)
            BattleManager.m_Instance.OnExpChanged -= HandleExpChanged;
    }

    private void HandleExpChanged(int _iCurrentExp, int _iMaxExp)
    {
        m_refExSliderImage.UpdateSlider(_iCurrentExp, _iMaxExp);
    }
    
    private void HandleExpSliderFilled()
    {
        BattleManager.m_Instance.LevelUp();
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.Q))
            Fire();
        else
            Cansle();
    }

    private void OnTriggerEnter(Collider other)
    {
        
    }

    private void OnDisable()
    {
        System.Array.Clear(m_arrNearCollider, 0, m_arrNearCollider.Length);
    }

    public void UpdateOnAnimation(eEntityState _eState, bool _bOn)
    {
        m_refAnimTable.SetBool(_eState, _bOn);
    }

    public void UpdateOnTriggerAnimation(eEntityState _eState)
    {
        m_refAnimTable.SetTrigger(_eState);
    }

    private void Fire()
    {
        bool bFindNearTarget = false;
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].gameObject.activeSelf == false)
                continue;

            if (m_listWeapon[i].CheckTime() == true)
            {
                if (m_listWeapon[i].NeeadNearTarget == true)
                    bFindNearTarget = true;

                m_listFireWeapon.Add(m_listWeapon[i]);
            }
        }


        if(bFindNearTarget == true)
            FindNearestTarget();

        Vector3 vTargetPos = m_refAim.TargetPosition;

        for (int i = 0; i < m_listFireWeapon.Count; ++i)
            m_listWeapon[i].Fire(vTargetPos, m_refNearTargetTr);

        m_listFireWeapon.Clear();
        m_refNearTargetTr = null;
    }

    private void Cansle()
    {
       
    }

    private void FindNearestTarget()
    {
        Physics.OverlapSphereNonAlloc(transform.position, m_fAttackRaius, m_arrNearCollider, m_tAttackLayer);

        Transform refTarget = null;
        float fBestDist = float.MaxValue;
        Vector3 vPos = transform.position;

        foreach (var refMon in m_arrNearCollider)
        {
            if (refMon == null)
                continue;

            float fDist = Vector3.SqrMagnitude(refMon.transform.position - vPos);
            if (fDist < fBestDist)
            {
                fBestDist = fDist;
                refTarget = refMon.transform;
            }
        }

        m_refNearTargetTr = refTarget;
    }


    private void Dead()
    {
        //플레이어가 죽었을 때
    }

    public void TakeDamage(AttackInfo _refAttackInfo)
    {
        if (m_CoNockback != null)
            StopCoroutine(m_CoNockback);

        m_refObjectInfo.CurrentHP -= _refAttackInfo.Damage;
        m_refHPSliderImage.UpdateSlider(m_refObjectInfo.CurrentHP, m_SOObjectInfo.MaxHP);


        m_CoNockback = StartCoroutine(CoNockback(_refAttackInfo));
    }

    private IEnumerator CoNockback(AttackInfo _refAttackInfo)
    {
        Vector3 vDir = _refAttackInfo.MoveDir;
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
    }

    // FeatureSO.Apply()에서 무기 해금 기능(예: SOFeatureUnlockMissile)이 호출
    // 씬에 미리 배치된 비활성 무기를 활성화하는 방식 (런타임 Instantiate 회피)
    public void UnlockWeapon(eWeaponType _eType)
    {
        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].WeaponType == _eType)
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
