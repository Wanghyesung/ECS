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


    private void Start()
    {
        //동적 데이터 생성
        m_refAttackInfo = m_SOAttackInfo?.MakeAttackInfo();

#if UNITY_EDITOR
        if (m_refAttackInfo == null)
            Debug.Log("무기에 공격 옵션SO를 설정하세요");
        //UnityEditor.EditorApplication.isPlaying = false;
#else
        // 실제 빌드된 PC/모바일 게임 환경에서는 프로세스를 종료합니다.
        Application.Quit();
#endif
    }

    public void Fire(Vector3 _vTargetPos, Transform _refTargetTr = null)
    {
        if (CheckTime() == false)
            return;

        GameObject refObj = ObjectPool.m_Instance.GetObject(m_SOAttackInfo.PoolType);
        if (refObj == null) 
            return;

        Bullet refAttackObj = refObj.GetComponent<Bullet>();
        if (refAttackObj == null)
            return;

        //각 무기에 맞는 파티클 실행
        Vector3 vFirePos = m_refFireTr.transform.position;
        refObj.transform.position = vFirePos;
        refAttackObj.transform.rotation = m_refFireTr.transform.rotation;

        m_refEffectObject?.Play();


        //공격력 전달, 방향 설정
        m_refAttackInfo.TargetPos = _vTargetPos;
        m_refAttackInfo.TargetTrasnform = _refTargetTr;
        m_fFireTime = m_refAttackInfo.CoolDown;

        refAttackObj.SetAttack(m_refAttackInfo);
    }

    private void Update()
    {
        if (m_fCurTime >= m_fFireTime)
            return;

        m_fCurTime += Time.deltaTime;
    }


    public bool CheckTime()
    {
        if(m_fCurTime > m_fFireTime)
        {
            m_fCurTime = 0.0f;
            return true;
        }

        return false;
    }
}

