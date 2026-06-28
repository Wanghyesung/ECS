using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI;
using UnityEngine.AI;


//상태이상 표현
public enum eStatusEffect
{
    Wait,
    Lock,
    Stun,
    Poison,
    Burn,
    End,
}

public enum eEntityState
{
    None,
    Idle,
    Move,
    Attack,
    Hit,
    Lock,
    Dead,
    End,
}


[Serializable]
public struct EffectEntry
{
    public float EndTime;
    public float TickInterval;
    public float NextTickTime;
    public long  TickDamage;
}

[Serializable]
public class ObjectInfo
{
    public eEntityState State;

    public long CurrentHP;
    public float Speed;

    public ushort CurrentEffects; //16비트로 전체 상태이상 표현
    public EffectEntry[] Effects = new EffectEntry[(int)eStatusEffect.End];
}
public class Player : MonoBehaviour
{
    [SerializeField] private List<Weapon> m_listWeapon = null;
    [SerializeField] private AnimationTable m_refAnimTable = null;
    [SerializeField] private Aim m_refAim= null;

    [SerializeField] private ObjectInfo m_refObjectInfo = new ObjectInfo();


    [SerializeField] LayerMask m_tAttackLayer;
    private Transform m_refNearTargetTr = null; 
    private Collider[] m_arrNearCollider = new Collider[20];
    [SerializeField] private float m_fAttackRaius;

    private void Awake()
    {

    }

    private void Update()
    {
        //Todo : 나중에 InputManager에서 Fire키가 눌렸는지 받아오는 방식으로
        if (Input.GetKey(KeyCode.Q))
            Fire();
        else
            Cansle();
    }

    private void OnTriggerEnter(Collider other)
    {
    }

    private void OnDisable()
    {
        System.Array.Clear(m_arrNearCollider, 0, m_arrNearCollider.Length);
    }

    public void UpdateOnAnimation(eEntityState _eState, bool _bOn)
    {
        m_refAnimTable.SetBool(_eState, _bOn);
    }

    public void UpdateOnTriggerAnimation(eEntityState _eState)
    {
        m_refAnimTable.SetTrigger(_eState);
    }

    private void Fire()
    {
        //내 총의 상태값
        //m_refAnimTable.SetBool(eEntityState.Attack, true);

        FindNearestTarget();
        Vector3 vTargetPos = m_refAim.TargetPosition;

        for(int i = 0; i< m_listWeapon.Count; ++i)
            m_listWeapon[i].Fire(vTargetPos, m_refNearTargetTr);
    }
    private void Cansle()
    {
        //m_refAnimTable.SetBool(eEntityState.Attack, false);
    }

    private void FindNearestTarget()
    {
        Physics.OverlapSphereNonAlloc(transform.position, m_fAttackRaius, m_arrNearCollider, m_tAttackLayer);

        Transform refTarget = null;
        float fBestDist = float.MaxValue;
        Vector3 vPos = transform.position;
        foreach (var refMon in m_arrNearCollider)
        {
            if (refMon == null)
                continue;

            float fDist = Vector3.SqrMagnitude(refMon.transform.position - vPos);
            if (fDist < fBestDist)
            {
                fBestDist = fDist;
                refTarget = refMon.transform;
            }
        }

        m_refNearTargetTr = refTarget;
    }

}
