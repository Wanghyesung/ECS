using UnityEngine;

/*///////////////////////////////////////////
            SORandomSelectorNode
기능 : 자식 노드 중 하나를 랜덤으로 선택해 실행
       타이머가 만료되면 새 자식을 랜덤 선택
       SOListNode이므로 클론되어 사용 → 인덱스/타이머를 노드 안에 직접 저장
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_RandomSelectNode", menuName = "Game/Monster/RandomSelectNode")]
public class SORandomSelectNode : SOListNode
{

    [SerializeField] private float m_fMinDuration = 1.0f;
    [SerializeField] private float m_fMaxDuration = 3.0f;

    private int m_iCurrentIdx = 0;
    private float m_fTimer = 0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (m_fTimer <= 0f)
        {
            m_iCurrentIdx = Random.Range(0, listNode.Count);
            m_fTimer = Random.Range(m_fMinDuration, m_fMaxDuration);
        }

        m_fTimer -= Time.deltaTime;

        eNodeState eState = listNode[m_iCurrentIdx].Execute(_refBB);

        if (eState == eNodeState.Failure)
            m_fTimer = 0f;

        return eState;
    }
}