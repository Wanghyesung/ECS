using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//동적 스탯


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
        None,
        Base,
        End,
    }

    [SerializeField] private eWeaponType m_eWeaponType;
    [SerializeField] private SOAttackInfo m_SOAttackInfo;

    [SerializeField] private Transform m_refFireTr = null;
    [SerializeField] private ParticleSystem m_refEffectObject;

    [SerializeField] private float m_fFireTime = 0.2f;
    private float m_fCurTime = 0.0f;


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
        refAttackObj.SetAttack(m_SOAttackInfo);

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
