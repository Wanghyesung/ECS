using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PoolObject;

/*///////////////////////////////////////////
                Missiles
기능 : 지정된 위치에 따라 회전하여 이동
 *///////////////////////////////////////////

public class Missiles : Bullet
{
    private Vector3 m_vTargetPosition;
    private float m_fTargetLength;

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void FixedUpdate()
    {
        UpdateDirMissile();
        base.FixedUpdate();
    }

    private void UpdateDirMissile()
    {
        if (m_refAttackInfo.TargetTrasnform == null)
            return;

        Vector3 vToTarget = m_vTargetPosition - transform.position;
        float fDist = vToTarget.magnitude;

        if (fDist > 0.1f)
        {
            Vector3 vDir = vToTarget / fDist; // 정규화

            // 내적 및 각도 계산 
            float fDot = Mathf.Clamp(Vector3.Dot(transform.forward, vDir), -1f, 1f);
            float fAngle = Mathf.Acos(fDot) * Mathf.Rad2Deg;

            float fAccRoateSpeed = (m_fTargetLength / fDist) * m_refAttackInfo.RotationSpeed * 0.5f;
            float fRotateSpeed = fAccRoateSpeed + m_refAttackInfo.RotationSpeed;

            float fStep = fRotateSpeed * Time.deltaTime;
            float t = (fAngle > 0.001f) ? Mathf.Clamp01(fStep / fAngle) : 1f;

            Vector3 vNewForward = Vector3.Slerp(transform.forward, vDir, t);
            m_refRigidbody.MoveRotation(Quaternion.LookRotation(vNewForward));

        }
    }

  
    protected override void AttackMonster(Collider other)
    {
        base.AttackMonster(other);
    }

    public override void SetAttack(AttackInfo _refAttackInfo)
    {
        base.SetAttack(_refAttackInfo);


        if (_refAttackInfo.TargetTrasnform == null)
            m_vTargetPosition = _refAttackInfo.TargetPos;
        else
            m_vTargetPosition = _refAttackInfo.TargetTrasnform.position;

        m_fTargetLength = (m_vTargetPosition - transform.position).magnitude;
    }



}
