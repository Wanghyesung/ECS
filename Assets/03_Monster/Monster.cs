using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public interface IDamageable
{
    public void TakeDamage(in tAttackInfo _refAttackInfo);
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

        m_refBlackBoard.State = eEntityState.Idle;
        m_refBlackBoard.Speed = m_SOMonsterInfo.Speed;
        m_refBlackBoard.CurrentHP = m_SOMonsterInfo.MaxHP;

    }

    private void Update()
    {
        m_refBT?.Evaluate(m_refBlackBoard);
    }

    public void TakeDamage(in tAttackInfo _refAttackInfo)
    {
        if (m_refBlackBoard.State == eEntityState.Hit)
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

}

