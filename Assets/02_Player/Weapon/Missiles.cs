using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PoolObject;

/*///////////////////////////////////////////
                Missiles
기능 : 몬스터를 자동으로 추격해주는 클래스
 *///////////////////////////////////////////

public class Missiles : Bullet
{
    private GameObject m_refTarget; //Monster

    private Collider[] m_arrNearCollider = new Collider[20];

    private Vector3 m_vTargetPosition;
    private float m_fTargetLength;

    protected override void Awake()
    {
        base.Awake();
    }


    protected  void Update()
    {
        if (m_refTarget == null)
            return;

        Vector3 vToTarget = m_vTargetPosition - transform.position;
        float fDist = vToTarget.magnitude;

        if (fDist > 0.1f)
        {
            Vector3 vDir = vToTarget / fDist; // 정규화
           
            // 내적 및 각도 계산 
            float fDot = Mathf.Clamp(Vector3.Dot(transform.forward, vDir), -1f, 1f);
            float fAngle = Mathf.Acos(fDot) * Mathf.Rad2Deg;

            float fAccRoateSpeed = (m_fTargetLength / fDist) * m_refAttackInfo.BaseRotationSpeed * 0.5f;
            float fRotateSpeed = fAccRoateSpeed + m_refAttackInfo.BaseRotationSpeed;

            float fStep = fRotateSpeed * Time.deltaTime;
            float t = (fAngle > 0.001f) ? Mathf.Clamp01(fStep / fAngle) : 1f;

            Vector3 vNewForward = Vector3.Slerp(transform.forward, vDir, t);
            transform.rotation = Quaternion.LookRotation(vNewForward);
        }
    }
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
    }
    
  
    protected override void AttackMonster(Collider other)
    {
        base.AttackMonster(other);
    }

    public override void SetAttack(SOAttackInfo _refAttackInfo)
    {
        base.SetAttack(_refAttackInfo);

        FindNearestTarget();

        if (m_refTarget == null)
            return;

        m_vTargetPosition = m_refTarget.transform.position;
        m_fTargetLength = (m_vTargetPosition - transform.position).magnitude;
    }


    private void FindNearestTarget()
    {
        Physics.OverlapSphereNonAlloc(transform.position, m_refAttackInfo.HomingRaius, m_arrNearCollider, m_refAttackInfo.HitLayers);

        GameObject refTarget = null;
        float fBestDist = float.MaxValue;
        Vector3 vPos = transform.position;
        foreach (var refMon in m_arrNearCollider)
        {
            if (refMon == null)
                continue;

            float fDist = Vector3.SqrMagnitude(refMon.transform.position - vPos);
            if (fDist < fBestDist)
            {
                fBestDist = fDist;
                refTarget = refMon.gameObject;
            }
        }

        m_refTarget = refTarget;
    }

}
