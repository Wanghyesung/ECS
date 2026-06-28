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
    //public List<GameObject> ListCurAttackObject;    //나중에 참조하거나 쓸 공격 오브젝트를 저장할 수 있기 위해서 사용
    public SpawnInfo CurrentAttackSpawn;

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
