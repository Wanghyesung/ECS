using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                VisualObject
기능 : 오브젝트의 물리적인 이동이 아닌, 오브젝트의 보이는 연출을 담당
*/

public class VisualObject : MonoBehaviour
{
    [SerializeField] private float m_fMaxRollAngle = 45f;
    [SerializeField] private float m_fRollSpeed = 5f;
    private float m_fCurrentRoll;

    [Header("Hit Shake")]
    [SerializeField] private float m_fHitShakeDuration = 0.35f;
    [SerializeField] private float m_fHitShakeFrequency = 30f;
    [SerializeField] private float m_fHitRollKick = 6f;
    [SerializeField] private float m_fHitPitchKick = 4f;

    private float m_fShakeElapsed = -1f; // 0 미만이면 비활성화
    private float m_fShakeRollKick;
    private float m_fShakePitchKick;

    private void Update()
    {
        float fX = InputManager.m_Instance.InputInfo.MoveDir.x;
        float fTargetRoll = fX * m_fMaxRollAngle;
        
        m_fCurrentRoll = Mathf.Lerp(m_fCurrentRoll, fTargetRoll, m_fRollSpeed * Time.deltaTime);
        Quaternion qRoll = Quaternion.Euler(0.0f, 0.0f, -m_fCurrentRoll);
        Quaternion qShake = UpdateHitShake();

        transform.localRotation = qRoll * qShake;
    }

    // 피격 연출 (넉백의 방향을 감안하여 회전), 방향에 따라 롤/피치 킥(흔들림)을 결정하는 함수
    public void PlayHitShake(Vector3 _vWorldKnockbackDir, float _fPower)
    {
        Transform refSpaceTr = transform.parent != null ? transform.parent : transform;
        Vector3 vLocalDir = refSpaceTr.InverseTransformDirection(_vWorldKnockbackDir);

        m_fShakeRollKick = -vLocalDir.x * m_fHitRollKick * _fPower;
        m_fShakePitchKick = vLocalDir.z * m_fHitPitchKick * _fPower;
        m_fShakeElapsed = 0f;
    }

    private Quaternion UpdateHitShake()
    {
        if (m_fShakeElapsed < 0f)
            return Quaternion.identity;

        m_fShakeElapsed += Time.deltaTime;
        float fRatio = m_fShakeElapsed / m_fHitShakeDuration;
        if (fRatio >= 1.0f)
        {
            m_fShakeElapsed = -1f;
            return Quaternion.identity;
        }

        float fDecay = 1.0f - fRatio;
        float fWobble = Mathf.Sin(m_fShakeElapsed * m_fHitShakeFrequency) * fDecay;
        float fBlend = fDecay * 0.6f + fWobble * 0.4f;

        float fPitch = m_fShakePitchKick * fBlend;
        float fRoll = m_fShakeRollKick * fBlend;

        return Quaternion.Euler(fPitch, 0.0f, fRoll);
    }
}
