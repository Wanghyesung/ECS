using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI;
using UnityEngine.AI;

public class Player : MonoBehaviour
{
    [SerializeField] private List<Weapon> m_listWeapon = null;
    [SerializeField] private AnimationTable m_refAnimTable = null;
    [SerializeField] private Aim m_refAim= null;

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
