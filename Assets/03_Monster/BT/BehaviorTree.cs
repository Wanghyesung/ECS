using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static Weapon;


public enum eNodeState
{
    Success,
    Failure,
    Running,
}


/*///////////////////////////////////////////
                  SONode
기능 : 노드 최상위 클래스 (모든 액션을 처리하는 단위)
 *///////////////////////////////////////////
public abstract class SONode : ScriptableObject
{
    public abstract eNodeState Execute(BlackBoard _refBB);
}

// SOList는 그냥 SONode 모음
public abstract class SOListNode : SONode
{
    [SerializeField] protected List<SONode> listNode = new List<SONode>();

    //SO는 공유 메모리이기 때문에 List의 현재 인덱스가 공유될 수 있다
    //때문에 List형태의 노드는 복사하여 사용
    public void CloneChildren(List<SOListNode> _ListTracker)
    {
        for (int i = 0; i < listNode.Count; i++)
        {
            if (listNode[i] is SOListNode listChild)
            {
                SOListNode clone = Instantiate(listChild);
                _ListTracker.Add(clone);
                listNode[i] = clone;
                clone.CloneChildren(_ListTracker);
            }
        }
    }
}


[Serializable]
public class BlackBoard
{
    [Header("Component")]
    public Monster Owner;
    public Transform TargetTr;

    [Header("EntityInfo")]
    public ObjectInfo ObjInfo;

    [Header("Trace")]
    public float CurrentTime;
    public float TraceTime;
    public float TraceMaxDistance;
    public float TraceMinDistance;
    public float POV;

    [Header("Attack")]
    public SpawnInfo CurrentAttackSpawn;

    //이동 타이머
    [Header("Strafe")]
    public Vector3 StrafeDir;
    public float StrafeTimer;

    [Header("RandomMove")]
    public int iRandomMoveIdx;
    public float RandomMoveTimer;
}




/*///////////////////////////////////////////
              BehaviorTree
 *///////////////////////////////////////////

public class BehaviorTree : MonoBehaviour
{
    [SerializeField] private SONode m_refRootNode = null;

    [SerializeField] private Monster m_refOwner;

    private bool m_bRunning = true;
    private readonly List<SOListNode> m_listClonedNodes = new List<SOListNode>();

    public void Awake()
    {
        if (m_refOwner == null)
            m_refOwner = GetComponent<Monster>();

        if (m_refRootNode is SOListNode rootList)
        {
            m_refRootNode = Instantiate(rootList);
            SOListNode clonedRoot = (SOListNode)m_refRootNode;
            m_listClonedNodes.Add(clonedRoot);
            clonedRoot.CloneChildren(m_listClonedNodes);
        }
    }

    private void OnDestroy()
    {
        foreach (SOListNode node in m_listClonedNodes)
        {
            if (node != null) Destroy(node);
        }
        m_listClonedNodes.Clear();
    }

    public bool StopBT() => m_bRunning = false;
    public bool StartBT() => m_bRunning = true;

    public void Evaluate(BlackBoard _refBB)
    {
        if (m_bRunning == true)
            m_refRootNode?.Execute(_refBB);
    }

}
