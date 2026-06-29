using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class Weapon : MonoBehaviour
{
    
    public enum eWeaponType
    {
        None,
        Bullet,
        Trace,
        Missile,
        End,
    }

    
    [SerializeField] private SOAttackInfo m_SOAttackInfo;

    private AttackInfo m_refAttackInfo;

    [SerializeField] private Transform m_refFireTr = null;
    [SerializeField] private ParticleSystem m_refEffectObject;

    private float m_fFireTime = 0.2f;
    private float m_fLastFireTime = -Mathf.Infinity;

    private eWeaponType m_eWeapoonType = eWeaponType.None;
    public eWeaponType WeaponType => m_eWeapoonType;

    public ePoolType FireBulletType => m_SOAttackInfo.PoolType;

    private void Start()
    {
        m_refAttackInfo = m_SOAttackInfo.MakeAttackInfo();
        m_fFireTime = m_refAttackInfo.CoolDown;
        m_eWeapoonType = m_SOAttackInfo.WeaponType;
        m_fLastFireTime = Time.time;

#if UNITY_EDITOR
        if (m_refAttackInfo == null)
            Debug.Log("공격 SO 에셋설정");
        //UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void Fire(Vector3 _vTargetPos, Transform _refTargetTr)
    {
        Bullet refBuulet = CreateBullet();

        refBuulet.transform.LookAt(_vTargetPos);
        refBuulet.transform.position = m_refFireTr.position;

       
        m_refAttackInfo.TargetPos = _vTargetPos;
        m_refAttackInfo.TargetTrasnform = _refTargetTr;
        refBuulet.SetAttack(m_refAttackInfo);
    }

    public void Fire(Vector3 _vTargetPos, Transform _refTargetTr, Vector3 _vDir, float _fFowardOffset)
    {
        Bullet refBuulet = CreateBullet();

        Quaternion qRoation = Quaternion.LookRotation(_vDir);
        refBuulet.transform.rotation = qRoation;
     
        Vector3 vSpawnPos = m_refFireTr.position + (_vDir * _fFowardOffset);
        refBuulet.transform.position = vSpawnPos;

        m_refAttackInfo.TargetPos = _vTargetPos;
        m_refAttackInfo.TargetTrasnform = _refTargetTr;
        refBuulet.SetAttack(m_refAttackInfo);
    }


    private Bullet CreateBullet()
    {
        GameObject refObj = ObjectPool.m_Instance.GetObject(m_SOAttackInfo.PoolType);
        if (refObj == null)
            return null;

        Bullet refAttackObj = refObj.GetComponent<Bullet>();
        if (refAttackObj == null)
            return null;

       
        if(m_refEffectObject != null)
            m_refEffectObject.Play();

        m_fLastFireTime = Time.time;
        m_fFireTime = m_refAttackInfo.CoolDown;

        return refAttackObj;
    }


    public bool CheckTime()
    {
        return (Time.time - m_fLastFireTime) > m_fFireTime;
    }
}

