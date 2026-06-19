using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttackObject : MonoBehaviour
{
    [SerializeField] private AttackState m_refAttackInfo = new AttackState();
    [SerializeField] eAttackOptionFlag m_eAttackOptFlag = eAttackOptionFlag.Base;


    // Update is called once per frame
    void Update()
    {
        transform.Translate(Vector3.forward * m_refAttackInfo.MoveSpeed * Time.deltaTime);


        Vector3 vPos = transform.position;
    }


    private void OnTriggerEnter(Collider other)
    {
        
    }

    public void SetAttack(AttackState _refAttackState)
    {
        m_refAttackInfo = _refAttackState;
    }

}
