using UnityEngine;

/*///////////////////////////////////////////
            SORandomSelectorNode
기능 : 자식 노드 중 하나를 랜덤으로 선택해 실행
       타이머가 만료되면 새 자식을 랜덤 선택
       타이머/인덱스는 BlackBoard에 저장 (SO 데이터 오염 방지)
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_RandomSelectNode", menuName = "Game/Monster/RandomSelectNode")]
public class SORandomSelectNode : SOListNode
{
    [SerializeField] private float m_fMinDuration = 1.0f;
    [SerializeField] private float m_fMaxDuration = 3.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (listNode.Count == 0)
            return eNodeState.Failure;

        if (_refBB.RandomMoveTimer <= 0f)
        {
            _refBB.iRandomMoveIdx = Random.Range(0, listNode.Count);
            _refBB.RandomMoveTimer = Random.Range(m_fMinDuration, m_fMaxDuration);
        }

        _refBB.RandomMoveTimer -= UnityEngine.Time.deltaTime;

        eNodeState eState = listNode[_refBB.iRandomMoveIdx].Execute(_refBB);

        if (eState == eNodeState.Failure)
            _refBB.RandomMoveTimer = 0f;

        return eNodeState.Running;
    }
}
