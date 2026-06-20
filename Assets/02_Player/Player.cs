using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI;
using UnityEngine.AI;

public class Player : MonoBehaviour
{
    private Animator m_refAnimator = null;
    [SerializeField] private Weapon m_refWeapon = null;
    [SerializeField] private AnimationTable m_refAnimTable = null;

    private void Awake()
    {
        m_refAnimator = GetComponent<Animator>();   
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
        m_refWeapon.Fire();
    }
    private void Cansle()
    {
        //m_refAnimTable.SetBool(eEntityState.Attack, false);
    }
}
