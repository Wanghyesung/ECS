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
        MissileBullet,
        Missile,
        Laser,
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

    [SerializeField] private bool m_bLookTarget = true;
    [SerializeField] private bool m_bNeedsTarget = false;
    public bool NeeadNearTarget => m_bNeedsTarget;

    private void Awake()
    {
        m_refAttackInfo = m_SOAttackInfo.MakeAttackInfo();
        m_fFireTime = m_refAttackInfo.CoolDown;
        m_eWeapoonType = m_SOAttackInfo.WeaponType;
        m_fLastFireTime = Time.time;

#if UNITY_EDITOR
        if (m_refAttackInfo == null)
            Debug.Log("공격 SO 에셋설정을 안 함");
        //UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    //바라는 방향으로 쏘지 않고 지정된 위치에만 소환
    //public void Spawn()
    //{
    //    GameObject refObj = CreateBullet();

    //    if (refObj == null)
    //        return;

    //    refObj.transform.position = m_refFireTr.position;
    //    //refObj.transform.rotation = m_refFireTr.rotation;

    //    IAttackObject refAttackObj = refObj.GetComponent<IAttackObject>();
    //    refAttackObj.SetAttack(m_refAttackInfo);
    //}

    public void Fire(Vector3 _vTargetPos, Transform _refTargetTr)
    {
        GameObject refObj = CreateBullet();

        if (refObj == null) 
            return;

        refObj.transform.position = m_refFireTr.position;
        if (m_bLookTarget == true)
            refObj.transform.LookAt(_vTargetPos);
        else
            refObj.transform.rotation = m_refFireTr.rotation;

        IAttackObject refAttackObj = refObj.GetComponent<IAttackObject>();
        m_refAttackInfo.TargetPos = _vTargetPos;
        m_refAttackInfo.TargetTrasnform = _refTargetTr;
        refAttackObj.SetAttack(m_refAttackInfo);
    }


    public void FireAndRotate(Vector3 _vDir, float _fFowardOffset)
    {
        GameObject refObj = CreateBullet();

        if (refObj == null)
            return;

        Vector3 vSpawnPos = m_refFireTr.position + (_vDir * _fFowardOffset);
        refObj.transform.position = vSpawnPos;

        Quaternion qRoation = Quaternion.LookRotation(_vDir);
        refObj.transform.rotation = qRoation;

        IAttackObject refAttackObj = refObj.GetComponent<IAttackObject>();
        refAttackObj.SetAttack(m_refAttackInfo);
    }


    private GameObject CreateBullet()
    {
        GameObject refObj = ObjectPool.m_Instance.GetObject(m_SOAttackInfo.PoolType);
        if (refObj == null)
            return null;

       
        if(m_refEffectObject != null)
            m_refEffectObject.Play();

        m_fLastFireTime = Time.time;
        m_fFireTime = m_refAttackInfo.CoolDown;

        return refObj;
    }


    public bool CheckTime()
    {
        return (Time.time - m_fLastFireTime) > m_fFireTime;
    }
}

