using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PoolObject;

/*///////////////////////////////////////////
                Missiles
기능 : 몬스터를 자동으로 추격해주는 클래스
 *///////////////////////////////////////////

public class Missiles : PlayerAttackObject
{
    private GameObject m_refTarget; //Monster

    private Collider[] m_arrNearCollider = new Collider[20];

    private Vector3 m_vTargetPosition;
    private float m_fTargetLength;



    protected override void Awake()
    {
        base.Awake();
    }

 

    protected override void Update()
    {
        base.Update();

        if (m_refTarget != null)
        {
            Vector3 vToTarget = m_vTargetPosition - transform.position;
            float fDist = vToTarget.magnitude;
            if (fDist > 0.01f)
            {
                Vector3 vDir = vToTarget / fDist;

                float fRatio = (1- vToTarget.magnitude / m_fTargetLength);
                float fRotSpeed = m_refAttackInfo.Speed * fRatio;

                // compute angle between forward and desired dir
                float fDot = Vector3.Dot(transform.forward.normalized, vDir.normalized);
                fDot = Mathf.Clamp(fDot, -1f, 1f);
                float fAngle = Mathf.Acos(fDot) * Mathf.Rad2Deg;

                float fStep = fRotSpeed * Time.deltaTime;
                float t = (fAngle > 0.001f) ? Mathf.Clamp01(fStep / fAngle) : 1f;

                Vector3 newForward = Vector3.Slerp(transform.forward, vDir, t);
                transform.rotation = Quaternion.LookRotation(newForward);
            }
        }
        else if (m_vTargetPosition.sqrMagnitude > 0.001f)
        {
            //여기서 공격 오브젝트 생성   
        }
    }

    protected override void SetOption(Vector3 _vDir)
    {
        m_vTargetPosition = _vDir.normalized;
        m_fTargetLength = (m_vTargetPosition - transform.position).magnitude;

        FindNearestTarget();
    }

    private void FindNearestTarget()
    {
        Physics.OverlapSphereNonAlloc(transform.position, m_refAttackInfo.HomingRaius, m_arrNearCollider, m_refAttackInfo.HitLayers);

        GameObject refTarget = null;
        float fBestDist = float.MinValue;
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


    protected override void FixedUpdate()
    {
        base.FixedUpdate();       
    }

    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);
    }
}
