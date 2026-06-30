using UnityEngine;

/*///////////////////////////////////////////
          SOParallelWaitNode

기능 : 모든 자식을 동시에 실행하되,
       Success를 반환한 자식은 재실행하지 않고 대기.
       모든 자식이 Success → Success 반환 후 리셋.
       Failure가 하나라도 나오면 → Failure 반환 후 리셋.

       SOParallelNode(루트용)와 달리, Charge+LookAt처럼
       '한 번 트리거 후 완료를 기다려야 하는' 노드 병행에 사용.
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_ParallelWaitNode", menuName = "Game/Monster/ParallelWaitNode")]
public class SOParallelWaitNode : SOListNode
{
    private bool[] m_arrDone;

    private void OnEnable()
    {
        m_arrDone = null;
    }

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (m_arrDone == null || m_arrDone.Length != listNode.Count)
            m_arrDone = new bool[listNode.Count];

        bool bAllDone = true;
        for (int i = 0; i < listNode.Count; ++i)
        {
            if (m_arrDone[i]) continue;

            eNodeState eState = listNode[i].Execute(_refBB);

            if (eState == eNodeState.Failure)
            {
                Reset();
                return eNodeState.Failure;
            }

            if (eState == eNodeState.Success)
                m_arrDone[i] = true;
            else
                bAllDone = false;
        }

        if (bAllDone)
        {
            Reset();
            return eNodeState.Success;
        }

        return eNodeState.Running;
    }

    private void Reset()
    {
        if (m_arrDone != null)
            System.Array.Clear(m_arrDone, 0, m_arrDone.Length);
    }
}
