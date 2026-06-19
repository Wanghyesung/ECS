using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDamageable
{
    public void TakeDamage();
}

public enum eEntityState
{
    None,
    Idle,
    Move,
    Attack,
    Hit,
    Dead,
    End,
}

public class MonsterState
{
    public int HP;
    public float Speed;

    public eEntityState State;
}

public class Monster : MonoBehaviour, IDamageable
{
    //정적 데이터 참조
    [SerializeField] private SOMonsterInfo m_refMonsterInfo;

    //내 동적 데이터 
    private MonsterState m_refState = new MonsterState();
    public eEntityState State => m_refState.State;

    private Coroutine m_CoNockback = null;

    private void Awake()
    {
        if (m_refMonsterInfo == null)
        {
            Debug.Log("몬스터 정보를 체워야함");
            
            return;
        }

        m_refState.State = eEntityState.Idle;
        m_refState.Speed = m_refMonsterInfo.Speed;
        m_refState.HP = m_refMonsterInfo.MaxHP;
    }

    //private void Start()
    //{

    //}

    //// Update is called once per frame
    //private void Update()
    //{

    //}

    public void TakeDamage(AttackInfo _refAttackInfo)
    {
        if (State == eEntityState.Hit)
            return;


        if(m_CoNockback != null)
            m_CoNockback = StartCoroutine(KnockbackCoroutine());
    }

    private IEnumerator KnockbackCoroutine(Vector3 _vDir, int _iPower)
    {
        float fElapsed = 0f;

        while (fElapsed <= 1.0f)
        {
            float fRevElaps = 1.0f - fElapsed;
            Vector3 vDelta = _vDir.normalized * _iPower * fRevElaps * Time.deltaTime;

            transform.position += vDelta;

            fElapsed += Time.fixedDeltaTime;

            yield return null;
        }

        m_pKnockbackRoutine = null;
    }

    //private void OnTriggerEnter(Collider other)
    //{
    //}


}

