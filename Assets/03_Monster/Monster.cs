using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public interface IDamageable
{
    public void TakeDamage(in tAttackInfo _refAttackInfo);
}


public class Monster : MonoBehaviour, IDamageable
{
    //TODO : 나중에 게임 매니저나 다른곳에서 전역으로 받을 수 있게
    [SerializeField] private GameObject m_refTargetPlayer;
    
    //정적 데이터 참조
    [SerializeField] private SOMonsterInfo m_SOMonsterInfo;

    //내 동적 데이터 
    private MonsterInfo m_refMonsterInfo = new MonsterInfo();

    public eEntityState State => m_refMonsterInfo.State;

    private Coroutine m_CoNockback = null;
    private WaitForSeconds m_refWaitForSecond = null;


    private NavMeshAgent m_refAgent = null;
    [SerializeField] private float m_fFindTime = 0.1f;
    private Coroutine m_CoFindPlayer = null;


    private void Awake()
    {
        if (m_SOMonsterInfo == null)
        {
            Debug.Log("몬스터 정보를 체워야함");
            
            return;
        }

        m_refMonsterInfo.State = eEntityState.Idle;
        m_refMonsterInfo.Speed = m_SOMonsterInfo.Speed;
        m_refMonsterInfo.HP = m_SOMonsterInfo.MaxHP;

        m_refAgent = GetComponent<NavMeshAgent>(); 
        m_refWaitForSecond = new WaitForSeconds(m_fFindTime);
    }

    private void Start()
    {
        m_CoFindPlayer = StartCoroutine(MoveCoroutine());
    }

    //// Update is called once per frame
    //private void Update()
    //{
    //}

    public void TakeDamage(in tAttackInfo _refAttackInfo)
    {
        if (State == eEntityState.Hit)
            return;


        if(m_CoNockback != null)
            StopCoroutine(m_CoNockback);

         m_CoNockback = StartCoroutine(KnockbackCoroutine(_refAttackInfo));

    }

    private IEnumerator KnockbackCoroutine(tAttackInfo _tAttackInfo)
    {
        float fElapsed = 0f;

        Vector3 vDir =  transform.position - _tAttackInfo.HitPosition;

        while (fElapsed <= 1.0f)
        {
            float fRevElaps = 1.0f - fElapsed;
            Vector3 vDelta = vDir.normalized * _tAttackInfo.AttackPower * fRevElaps * Time.deltaTime;

            transform.position += vDelta;

            fElapsed += Time.fixedDeltaTime;

            yield return null;
        }

        m_CoNockback = null;
    }

    private IEnumerator MoveCoroutine()
    {
        while(true)
        {
            if (m_refMonsterInfo.State == eEntityState.Dead ||
                m_refMonsterInfo.State == eEntityState.Hit)
                break;

            //플레이어 상태확인

            m_refAgent?.SetDestination(m_refTargetPlayer.transform.position);

            yield return m_refWaitForSecond;
        }
    }


    //private void OnTriggerEnter(Collider other)
    //{
    //}


}

