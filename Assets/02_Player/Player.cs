using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI;
using UnityEngine.AI;

public class Player : MonoBehaviour
{
    private Animator m_refAnimator = null;
    [SerializeField] private Weapon m_refWeapon = null;


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

    private void OnTriggerStay(Collider other)
    {
        
    }


    private void OnTriggerExit(Collider other)
    {
    }

    public void UpdateOnAnimation(string _strAnimName, bool _bOn)
    {
        m_refAnimator.SetBool(_strAnimName, _bOn);
    }

    public void UpdateOnTriggerAnimation(string _strAnimName)
    {
        m_refAnimator.SetTrigger(_strAnimName);
    }

    private void Fire()
    {
        //내 총의 상태값
        UpdateOnAnimation("bAttack", true);
        m_refWeapon.Fire();
    }
    private void Cansle()
    {
        UpdateOnAnimation("bAttack", false);
    }
}
