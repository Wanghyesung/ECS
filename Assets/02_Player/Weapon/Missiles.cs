using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/*///////////////////////////////////////////
                Missiles
기능 : 지정된 위치로 회전하면서 목적지로 이동 후 공격
 *///////////////////////////////////////////

public class Missiles : Bullet
{
    private float m_fElapsedTime;
    [SerializeField] private PoolObject m_refExplodeObj;
    [SerializeField] private bool m_bTraceTarget = false;

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
        Vector3 vTargetPos = Vector3.zero;
   
        if (m_bTraceTarget == true)
            vTargetPos = m_refAttackInfo.TargetTrasnform.position;
        else
            vTargetPos = m_refAttackInfo.TargetPos;

        Vector3 vToTarget = vTargetPos - transform.position;
        float fDist = vToTarget.magnitude;

        // 이번 스텝에 실제로 이동할 거리보다 남은 거리가 짧으면 목표를 지나치기 전에 도착 처리
        // (고정 임계값만 쓰면 스텝이 임계값보다 커지는 순간 목표를 터널링해서 영원히 도착 판정이 안 남)
        float fMoveDist = m_refAttackInfo.AttackSpeed * Time.fixedDeltaTime;
        if (fDist <= Mathf.Max(fMoveDist, 1.0f))
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
        float fDistAccel = /*(fTargetLength / fDist) * */fBaseSpeed * 0.5f;
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
    }

    private void SpawnExplosion()
    {
        if (m_refExplodeObj != null)
        {
            GameObject refExObject = ObjectPool.m_Instance.GetObject(m_refExplodeObj);
            refExObject.transform.position = transform.position;
        }
    }
}
