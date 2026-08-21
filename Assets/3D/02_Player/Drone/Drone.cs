using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                    Drone
기능 : 플레이어를 따라 몬스터를 공격하는 오브젝트
 *///////////////////////////////////////////

public class Drone : MonoBehaviour
{
    [SerializeField] private TargetScanner m_refTargetScanner;

    [SerializeField] private Weapon m_refWeapon;

    [SerializeField] private float m_fRotateSpeed;

    [SerializeField] private float m_fFireAngle = 10f; // 사격을 허용할 각도 오차 범위

    private void Awake()
    {
        m_refWeapon?.Init();
    }
    private void Update()
    {
        Transform refTarget = m_refTargetScanner.Target;
        if (refTarget == null)
            return;

        Vector3 vTargetDir = (refTarget.position - transform.position).normalized;
        Quaternion qTargetRot = Quaternion.LookRotation(vTargetDir);

        transform.rotation = Quaternion.RotateTowards(transform.rotation, qTargetRot, m_fRotateSpeed * Time.deltaTime);
        // 드론의 정면(forward)과 타겟을 향한 방향의 사이 각도 계산
        float fAngleDiff = Vector3.Angle(transform.forward, vTargetDir);

        if(m_fFireAngle < fAngleDiff)
            Fire();
    }

    private void Fire()
    {
        Transform refTarget = m_refTargetScanner.Target;
        m_refWeapon.UpdateWeapon(refTarget.position, refTarget);
    }
 
}
