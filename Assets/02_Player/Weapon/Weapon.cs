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
    private float m_fCurTime = 0.0f;

    private eWeaponType m_eWeapoonType = eWeaponType.None;
    public eWeaponType WeaponType => m_eWeapoonType;

    public ePoolType FireBulletType => m_SOAttackInfo.PoolType;

    private void Start()
    {
        //동적 데이터 생성
        m_refAttackInfo = m_SOAttackInfo.MakeAttackInfo();
        m_fFireTime = m_refAttackInfo.CoolDown;
        m_eWeapoonType = m_SOAttackInfo.WeaponType;

#if UNITY_EDITOR
        if (m_refAttackInfo == null)
            Debug.Log("무기에 공격 옵션SO를 설정하세요");
        //UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Update()
    {
        if (m_fCurTime >= m_fFireTime)
            return;

        m_fCurTime += Time.deltaTime;
    }

    public void Fire(Vector3 _vTargetPos, Transform _refTargetTr)
    {
        Bullet refBuulet = CreateBullet();

        refBuulet.transform.LookAt(_vTargetPos);
        refBuulet.transform.position = m_refFireTr.position;

        //공격력 전달, 방향 설정
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

        //각 무기에 맞는 파티클 실행
        if(m_refEffectObject != null)
            m_refEffectObject.Play();

        //총 발사 시간 설정
        m_fFireTime = m_refAttackInfo.CoolDown;

        return refAttackObj;
    }

  
    public bool CheckTime()
    {
        if(m_fCurTime > m_fFireTime)
        {
            m_fCurTime = 0;
            return true;
        }
        

        return false;
    }
}

