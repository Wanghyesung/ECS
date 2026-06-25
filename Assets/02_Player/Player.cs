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
public class ObjectInfo
{
    public eEntityState State;
    public ushort CurrentEffects; //16비트로 전체 상태이상 표현

    public long CurrentHP;
    public float Speed;

    //상태이상 체크 타이머
    public float[] EffectTimes = new float[(int)eStatusEffect.End];
}
public class Player : MonoBehaviour
{
    [SerializeField] private List<Weapon> m_listWeapon = null;
    [SerializeField] private AnimationTable m_refAnimTable = null;
    [SerializeField] private Aim m_refAim= null;

    [SerializeField] private ObjectInfo m_refObjectInfo = new ObjectInfo();

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
        int a = 10;    
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


        Vector3 vTargetPos = m_refAim.TargetPosition;
        for(int i = 0; i< m_listWeapon.Count; ++i)
            m_listWeapon[i].Fire(vTargetPos);
    }
    private void Cansle()
    {
        //m_refAnimTable.SetBool(eEntityState.Attack, false);
    }
}
