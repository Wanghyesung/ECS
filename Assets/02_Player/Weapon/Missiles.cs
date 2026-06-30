using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static PoolObject;

/*///////////////////////////////////////////
                Missiles
기능 : 지정된 위치로 회전하면서 목적지로 이동 후 공격
 *///////////////////////////////////////////

public class Missiles : Bullet
{
    private Vector3 m_vTargetPosition;
    private float m_fTargetLength;
    private float m_fElapsedTime;

    [SerializeField] private ePoolType m_eExplosionType = ePoolType.None;

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void FixedUpdate()
    {
        UpdateDirMissile();
        base.FixedUpdate();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        m_refPoolObj.OnPush += SpawnExplosion;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        m_refPoolObj.OnPush -= SpawnExplosion;
    }

    private void UpdateDirMissile()
    {
        if (m_refAttackInfo.TargetTrasnform == null)
            return;

        m_fElapsedTime += Time.fixedDeltaTime;

        Vector3 vToTarget = m_vTargetPosition - transform.position;
        float fDist = vToTarget.magnitude;

        if(fDist < 1.0f)
        {
            m_refPoolObj.SetAliveTime(0.0f);
            return;
        }

        Vector3 vDir = vToTarget / fDist;

        float fDot = Mathf.Clamp(Vector3.Dot(transform.forward, vDir), -1f, 1f);
        float fAngle = Mathf.Acos(fDot) * Mathf.Rad2Deg;

        // 시간 기반 가속 + 거리 기반 가속 합산, MaxRotationSpeed로 상한
        float fTimeAccel = m_refAttackInfo.RotateSpeedRate * m_fElapsedTime;
        float fBaseSpeed = Mathf.Min(m_refAttackInfo.RotationSpeed + fTimeAccel, m_refAttackInfo.MaxRotationSpeed);
        float fDistAccel = (m_fTargetLength / fDist) * fBaseSpeed * 0.5f;
        float fRotateSpeed = fBaseSpeed + fDistAccel;

        float fStep = fRotateSpeed * Time.fixedDeltaTime;
        float t = (fAngle > 0.001f) ? Mathf.Clamp01(fStep / fAngle) : 1f;

        Vector3 vNewForward = Vector3.Slerp(transform.forward, vDir, t);
        m_refRigidbody.MoveRotation(Quaternion.LookRotation(vNewForward));
    }

  
    protected override void AttackMonster(Collider other)
    {
        base.AttackMonster(other);
    }

    public override void SetAttack(AttackInfo _refAttackInfo)
    {
       base.SetAttack(_refAttackInfo);  


        m_fElapsedTime = 0f;

        if (_refAttackInfo.TargetTrasnform == null)
            m_vTargetPosition = _refAttackInfo.TargetPos;
        else
            m_vTargetPosition = _refAttackInfo.TargetTrasnform.position;

        m_fTargetLength = (m_vTargetPosition - transform.position).magnitude;
    }

    private void SpawnExplosion()
    {
        if(m_eExplosionType != ePoolType.None)
        {
            //현재 위치에서 소환
            GameObject refExObject = ObjectPool.m_Instance.GetObject(m_eExplosionType);
            refExObject.transform.position = transform.position;
        }
    }


}
