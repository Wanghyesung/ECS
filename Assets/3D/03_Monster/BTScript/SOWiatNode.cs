using UnityEngine;

/*///////////////////////////////////////////
                 WaitNode
기능 : 지정한 시간(m_fWaitSeconds)만큼 대기 후 Success 반환
       진행 시간은 SO(공유 에셋)가 아닌 BlackBoard.WaitTimer에 저장 (SO 데이터 오염 방지)
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_WaitNode", menuName = "Game/Monster/ActionNode/WaitNode")]
public class SOWiatNode : SONode
{
    [SerializeField] private float m_fWaitSeconds = 1f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        _refBB.WaitTimer += Time.deltaTime;

        if (_refBB.WaitTimer < m_fWaitSeconds)
            return eNodeState.Running;

        _refBB.WaitTimer = 0f;
        return eNodeState.Success;
    }
}
