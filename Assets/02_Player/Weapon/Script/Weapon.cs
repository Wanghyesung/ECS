using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//동적 스탯
[Serializable]
public class AttackState
{
    public int AttackDamage;
    public int AttackPower;

    public float StunTime;

    public float MoveSpeed;

    public Vector3 
}



[System.Flags]
public enum eAttackOptionFlag
{
    Base = 0,
    Random = 1 << 0,
}

public class Weapon : MonoBehaviour
{
    public enum eWeaponType
    {
        None = 0,
        AR1 = 1,
        AR2 = 2,
        AR_B = 3,
        AR_C = 4,
        AR_D = 5,
        AR_E = 6,
        AR_END =10,
    }

    [SerializeField] private eWeaponType m_eWeaponType;
    [SerializeField] private AttackState m_refAttackState = new AttackState();

    [SerializeField] private Transform m_refFireTr = null;
    [SerializeField] private ParticleSystem m_refEffectObject;

    [SerializeField] private float m_fFireTime = 0.4f;
    private float m_fCurTime = 0.0f;

    private bool m_bFireReady = false;

    public AttackState GetAttackState() => m_refAttackState;
    public void SetAttackInfo(int _iAttackDamage, int _iAttackPower, float _fStunTime)
    {
        m_refAttackState.AttackDamage = _iAttackDamage;
        m_refAttackState.AttackPower = _iAttackPower;
        m_refAttackState.StunTime = _fStunTime;
    }

  
    public virtual void Fire()
    {
        if (CheckTime() == false)
            return;

        GameObject refObj = ObjectPool.m_Instance.GetObject(PoolObject.ePoolType.BaseBullet);
        if (refObj == null) 
            return;

        PlayerAttackObject refAttackObj = refObj.GetComponent<PlayerAttackObject>();
        if (refAttackObj == null)
            return;

        //공격력 전달
        refAttackObj.SetAttack(m_refAttackState);

        //각 무기에 맞는 파티클 실행
        Vector3 vFirePos = m_refFireTr.transform.position;
        refObj.transform.position = vFirePos;
        refAttackObj.transform.rotation = m_refFireTr.transform.rotation;

        m_refEffectObject.Play();
    }

    private void Update()
    {
        if (m_fCurTime >= m_fFireTime)
            return;

        m_fCurTime += Time.deltaTime;
    }


    private bool CheckTime()
    {
        if(m_fCurTime > m_fFireTime)
        {
            m_fCurTime = 0.0f;
            return true;
        }

        return false;
    }

}
