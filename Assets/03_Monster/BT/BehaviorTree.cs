using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.AI;


public enum eNodeState
{
    Success,
    Failure,
    Running,
}


/*///////////////////////////////////////////
                  SONode
설명 : 가장 최상위 노드 (어떠한 행동을 실행하는 역할)
 *///////////////////////////////////////////
public abstract class SONode : ScriptableObject
{
    public abstract eNodeState Execute(BlackBoard _refBB);
}

// SOList도 그냥 SONode 취급
public abstract class SOListNode : SONode
{
    [SerializeField] protected List<SONode> listNode = new List<SONode>();
}


[Serializable]
public class BlackBoard
{
    [Header("Component")]
    public Monster Owner;
    public Transform TargetTr;
    public Animator Animator;
    public NavMeshAgent Agent;

    [Header("EntityInfo")]
    public ObjectInfo ObjInfo;

    [Header("Trace")]
    public float CurrentTime;
    public float TraceTime;
    public float TraceMaxDistance;
    public float TraceMinDistance;
    public float POV;

    [Header("Attack")]
    public List<float> ListCurAttackTime;
    public int CurrentAttackIdx;
    public double CurrentAttackTime;
}

/*///////////////////////////////////////////
              BehaviorTree
 *///////////////////////////////////////////

public class BehaviorTree : MonoBehaviour
{
    [SerializeField] private SONode m_refRootNode = null;

    [SerializeField] private Monster m_refOwner;

    private bool m_bRunning = true;

    public void Awake()
    {
        if(m_refOwner == null)
            m_refOwner = GetComponent<Monster>();
    }

    public bool StopBT() => m_bRunning = false;
    public bool StartBT() => m_bRunning = true;

    public void Evaluate(BlackBoard _refBB)
    {
        if (m_bRunning == true)
            m_refRootNode?.Execute(_refBB);
    }

}
