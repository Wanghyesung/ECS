using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static Weapon;

public interface IDamageable
{
    public void TakeDamage(AttackInfo _refAttackInfo, tShotInfo _refShotInfo);
}

public interface IChangeInfoable
{
    public ObjectInfo ObjectInfo { get; }
    public void ChangeHPRatio(float _fRatio);
    public void UpHPRatio(float _fRatio);
    public void AddHP(long _lValue);
    public void ChangeSpeedRatio(float _fRatio);
    public void UpAttackRatio(float _fRatio);
    public void AddAttack(int _iValue);
    public void UpSpeedRatio(float _fRatio);
    public void AddSpeed(float _fValue);
    public void UpDefenseRatio(float _fRatio);
    public void AddDefense(float _fValue);
}

/*///////////////////////////////////////////
                 SpawnInfo

기능 : 몬스터 사용할 공격 오브젝트 및 스폰 관리
 *///////////////////////////////////////////

[Serializable]
public class SpawnInfo
{
    public Weapon Weapon;

    [Header("Spawn Option")]
    public float SpawnFowardOffset;
    public int SpawnCount;

    [Header("Charge Option")]
    public ParticleSystem ChargeParticle;
    public Transform SpawnParticleTransform;
    public float SpawnWaitTime; //Charge
}

/*///////////////////////////////////////////
                 Monster

기능 : BT에 따라 몬스터 행동 조작, 공격 오브젝트 관리
 *///////////////////////////////////////////

public class Monster : MonoBehaviour, IDamageable
{ 
  
    [SerializeField] private GameObject m_refTargetPlayer;
    [SerializeField] private VisualObject m_refVisualObj = null;

    [SerializeField] private SOMonsterInfo m_SOMonsterInfo;

    [SerializeField] private BlackBoard m_refBlackBoard = new BlackBoard();
    [SerializeField] private BehaviorTree m_refBT = null;

    [SerializeField] private List<SpawnInfo> m_listSpawn = new List<SpawnInfo>();
 

    //같은 웨폰이 여러개일 수 있기 때문에 List
    private Dictionary<eWeaponType, List<SpawnInfo>> m_hashSpawn = new Dictionary<eWeaponType, List<SpawnInfo>>();
    public Dictionary<eWeaponType, List<SpawnInfo>> HashSpawn => m_hashSpawn;

    private Coroutine m_CoNockback = null;

    private PoolObject m_refPoolObj;

    //몬스터가 죽을 때 생성되는 연출
    [SerializeField] private PoolObject m_refDeadEffect;

    // DeadEffect 크기 비례용 - 모듈형 파츠(Wing/MainBody 등) 전체 Renderer를 합산한 바운드 크기.
    private float m_fMonsterSize = 0f;
    // BattleManager가 구독해서 EXP 누적에 사용 (HP는 각 몬스터 인스턴스가 전담, 사망 알림만 정적 이벤트로 공유)
    public static event Action<int> OnMonsterDied;

    private void Awake()
    {
        if (m_SOMonsterInfo == null)
        {
            Debug.Log("몬스터 정보가 없습니다");
            return;
        }

        if(m_refBT == null)
            m_refBT = GetComponent<BehaviorTree>();

        m_refPoolObj = GetComponent<PoolObject>();

        m_refBlackBoard.Owner = this;

        for(int i = 0; i< m_listSpawn.Count; ++i)
            m_listSpawn[i].Weapon.Init();

        CacheMonsterSize();
    }

    private void CacheMonsterSize()
    {
        Renderer[] arrRenderers = GetComponentsInChildren<Renderer>();
        if (arrRenderers.Length == 0)
            return;

        Bounds tBounds = arrRenderers[0].bounds;
        for (int i = 1; i < arrRenderers.Length; ++i)
            tBounds.Encapsulate(arrRenderers[i].bounds);

        m_fMonsterSize = tBounds.size.magnitude;
    }
    
    private void OnEnable()
    {
        m_refBlackBoard.ObjInfo.State = eEntityState.Idle;
        m_refBlackBoard.ObjInfo.Speed = m_SOMonsterInfo.MaxSpeed; //Range로 잡기
        m_refBlackBoard.ObjInfo.CurrentHP = m_SOMonsterInfo.MaxHP;
        

        if (m_refPoolObj != null)
            m_refPoolObj.OnPush += ResetState;
    }

    private void OnDisable()
    {
        if (m_refPoolObj != null)
            m_refPoolObj.OnPush -= ResetState;
    }

    // 풀에 반납되는 시점(OnPush)에 실행. OnEnable의 State/Speed/HP 리셋만으로는 안 지워지는
    // "이전 생"의 잔여물(넉백 코루틴, 상태이상 비트마스크)을 정리해 재사용 시 새는 걸 막음
    private void ResetState()
    {
        if (m_CoNockback != null)
        {
            StopCoroutine(m_CoNockback);
            m_CoNockback = null;
        }

        m_refBlackBoard.ObjInfo.CurrentEffects = 0;
    }

    private void Start()
    {
        m_refPoolObj.SetAliveTime(float.MaxValue);
        // DeleteTem
        var player = FindObjectOfType<Player>();
        if (player == null) 
            return;

        m_refTargetPlayer = player.gameObject;
        m_refBlackBoard.TargetTr = player.transform;
        
        int iCount = m_listSpawn.Count;
        if (iCount > 0)
        {
            for (int i = 0; i < iCount; ++i)
            {
                Weapon refWeapon = m_listSpawn[i].Weapon;
                
                if (m_hashSpawn.TryGetValue(refWeapon.WeaponType,out var listWeapon) == false)
                {
                    var listSpawn = new List<SpawnInfo>();
                    m_hashSpawn.Add(refWeapon.WeaponType, listSpawn);
                }
                m_hashSpawn[refWeapon.WeaponType].Add(m_listSpawn[i]);
            }
        }
    }

    private void Update()
    {
        if (m_refBlackBoard.ObjInfo.State == eEntityState.Dead)
            return;

        m_refBT?.Evaluate(m_refBlackBoard);
    }

    public void TakeDamage(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        if (/*m_refBlackBoard.ObjInfo.State == eEntityState.Hit ||*/ m_refBlackBoard.ObjInfo.State == eEntityState.Dead)
            return;

        m_refBlackBoard.ObjInfo.CurrentHP -= _refAttackInfo.Damage;
        MonsterHPBar.m_Instance?.ShowHp(this, m_refBlackBoard.ObjInfo.CurrentHP, m_SOMonsterInfo.MaxHP);

        if (m_refBlackBoard.ObjInfo.CurrentHP <= 0)
        {
            Dead();
            GameObject refDeadEffect = ObjectPool.m_Instance.GetObject(m_refDeadEffect, transform.position);
            refDeadEffect.GetComponent<HitEffect>()?.SetSize(m_fMonsterSize);

            return;
        }

        //TODO BossMonster과 차별점을 생각
        if(m_CoNockback !=null)
            StopCoroutine(m_CoNockback);

        m_CoNockback = StartCoroutine(CoNockback(_refAttackInfo, _refShotInfo));
    }

    private void Dead()
    {
        //m_refVisualObj.RollX = 1;//여기 문제

        ChangeState(eEntityState.Dead);
        OnMonsterDied?.Invoke(m_SOMonsterInfo.ExpReward);

        if (m_refPoolObj != null)
            ObjectPool.m_Instance.PushObject(gameObject);
        else
            gameObject.SetActive(false);
    }

    private IEnumerator CoNockback(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        ChangeState(eEntityState.Hit);


        Vector3 vDir = _refShotInfo.MoveDir;
        float fPower = _refAttackInfo.AttackPower;
        m_refVisualObj?.PlayHitShake(vDir, fPower);

        float fDuration = Mathf.Max(_refAttackInfo.KnockbackDuration, 0.0001f);
        float fNockPower = _refAttackInfo.KnockbackForce;
        float fElapsed = 0f;

        while (fElapsed < fDuration)
        {
            float fRevElaps = 1.0f - (fElapsed / fDuration);
            Vector3 vDelta = vDir * fNockPower * fRevElaps * Time.deltaTime;
            transform.position += vDelta;

            fElapsed += Time.deltaTime;
            yield return null;
        }

        ChangeState(eEntityState.Idle);
        m_CoNockback = null;
    }


    // _fTickInterval > 0 이면 DoT 효과 (독, 화상 등), 0이면 단순 상태 이상
    public void StartStateEffect(eStatusEffect _eState, float _fDuration,
                                  float _fTickInterval = 0f, long _lTickDamage = 0)
    {
        m_refBlackBoard.ObjInfo.CurrentEffects =
            (ushort)(m_refBlackBoard.ObjInfo.CurrentEffects | (1 << (ushort)_eState));

        int idx = (int)_eState;
        m_refBlackBoard.ObjInfo.Effects[idx].EndTime      = Time.time + _fDuration;
        m_refBlackBoard.ObjInfo.Effects[idx].TickInterval = _fTickInterval;
        m_refBlackBoard.ObjInfo.Effects[idx].TickDamage   = _lTickDamage;
        m_refBlackBoard.ObjInfo.Effects[idx].NextTickTime =
            _fTickInterval > 0f ? Time.time + _fTickInterval : float.MaxValue;
    }

    // 효과가 만료됐으면 true (SOWaitStatusEffectNode에서 사용)
    public bool CheckStateEffect(eStatusEffect _eState)
    {
        if (m_refBlackBoard.ObjInfo.Effects[(int)_eState].EndTime <= Time.time)
        {
            m_refBlackBoard.ObjInfo.CurrentEffects =
                (ushort)(m_refBlackBoard.ObjInfo.CurrentEffects & ~(1 << (ushort)_eState));
            return true;
        }
        return false;
    }


    public void ChangeState(eEntityState _eState)
    {
        if (m_refBlackBoard.ObjInfo.State == _eState)
            return;

        m_refBlackBoard.ObjInfo.State = _eState;
    }

}

