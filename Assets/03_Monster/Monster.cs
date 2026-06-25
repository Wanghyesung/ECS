using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public interface IDamageable
{
    public void TakeDamage(in tAttackInfo _refAttackInfo);
}


[Serializable]
public class SpawnInfo
{
    public SOSpawnObjectInfo SpawnObjectInfo;

    [Header("Spawn Option")]
    public Transform SpawnTransform;
    public float SpawnOffset;


    [Header("Charge Option")]
    public ParticleSystem ParticleSys;
    public Transform SpawnParticleTransform;
    public float SpawnWaitTime; //Charge


}

/*///////////////////////////////////////////
                 Monster

기능 : BT행동 제어, 몬스터 상태 관리
 *///////////////////////////////////////////

public class Monster : MonoBehaviour, IDamageable
{
    //TODO : 나중에 게임 매니저나 다른곳에서 전역으로 받을 수 있게
    [SerializeField] private GameObject m_refTargetPlayer;
    
    //정적 데이터 참조
    [SerializeField] private SOMonsterInfo m_SOMonsterInfo;

    //내 동적 데이터 
    [SerializeField] private BlackBoard m_refBlackBoard = new BlackBoard();
    [SerializeField] private BehaviorTree m_refBT = null;

    [SerializeField] private List<SpawnInfo> m_listSpawnObject = new List<SpawnInfo>();
    public List<SpawnInfo> ListSpawnObject => m_listSpawnObject;

    private Coroutine m_CoNockback = null;
    private WaitForSeconds m_refWaitHitTime;

    private void Awake()
    {
        if (m_SOMonsterInfo == null)
        {
            Debug.Log("몬스터 정보를 체워야함");
            return;
        }

        if(m_refBT == null)
            m_refBT = GetComponent<BehaviorTree>(); 

        m_refBlackBoard.ObjInfo.State = eEntityState.Idle;
        m_refBlackBoard.ObjInfo.Speed = m_SOMonsterInfo.Speed;
        m_refBlackBoard.ObjInfo.CurrentHP = m_SOMonsterInfo.MaxHP;
    }

    private void Start()
    {
        // DeleteTem
        m_refTargetPlayer = FindObjectOfType<Player>().gameObject;
    }


    private void OnEnable()
    {
        int iCount = m_listSpawnObject.Count;
        if (iCount > 0)
        {
            m_refBlackBoard.ListCurAttackTime = new List<float>(iCount);
            m_refBlackBoard.ListCurAttackObject = new List<GameObject>();

            for(int i = 0; i< iCount; ++i)
            {
                m_refBlackBoard.ListCurAttackTime.Add(0.0f);
                m_refBlackBoard.ListCurAttackTime[i] = Time.time + m_listSpawnObject[i].SpawnObjectInfo.SpawnTime;
            }
        }
    }

    private void Update()
    {
        m_refBlackBoard.CurrentAttackTime += Time.deltaTime;
        m_refBT?.Evaluate(m_refBlackBoard);
    }

    public void TakeDamage(in tAttackInfo _refAttackInfo)
    {
        if (m_refBlackBoard.ObjInfo.State == eEntityState.Hit)
            return;

        if(m_CoNockback !=null)
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

    public SOSpawnObjectInfo GetSpawnObject(int _iIdx)
    {
        if (m_listSpawnObject.Count >= _iIdx)
            return null;

        return m_listSpawnObject[_iIdx].SpawnObjectInfo;
    }

}

