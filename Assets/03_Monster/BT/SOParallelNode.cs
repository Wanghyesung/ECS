using UnityEngine;

/*///////////////////////////////////////////
              SOParallelNode

기능 : 모든 자식 노드를 매 프레임 실행.
       한 자식이 Failure여도 나머지를 계속 실행.
       GC Alloc 0 보장 — SO 자체에 상태 없음, 모든 상태는 BlackBoard에.

       BT 최상단에 두고 [패시브 효과 처리 노드, 기존 행동 트리 루트] 순서로 구성.
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_ParallelNode", menuName = "Game/Monster/ParallelNode")]
public class SOParallelNode : SOListNode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        bool bAnyRunning = false;
        for (int i = 0; i < listNode.Count; i++)
        {
            eNodeState state = listNode[i].Execute(_refBB);
            if (state == eNodeState.Running)
                bAnyRunning = true;
        }
        return bAnyRunning ? eNodeState.Running : eNodeState.Success;
    }
}
