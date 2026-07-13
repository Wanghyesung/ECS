using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                VisualObject
기능 : 오브젝트의 물리적인 이동이 아닌, 오브젝트의 보이는 연출을 담당
*////////////////////////////////////////////

public class VisualObject : MonoBehaviour
{
    private Vector3 m_vRollDir = Vector3.zero;
    public Vector3 RollDir
    {
        get { return m_vRollDir; }
        set { m_vRollDir = value; }
    }

    public float RollX{ set{m_vRollDir.z = value;}}
    public float RollY{ set{m_vRollDir.z = value;}}
    public float RollZ{ set{m_vRollDir.z = value;}}

    [SerializeField] private float m_fMaxRollAngle = 180f;
    [SerializeField] private float m_fRollSpeed = 5f;
    private float m_fCurrentRollZ;
    private float m_fCurrentRollX;

    [Header("Max Roll (Barrel Roll)")]
    private float m_fMaxRollElapsed = -1f; // 0 미만이면 비활성화
    private float m_fMaxRollDuration = 0.5f;
    private float m_fMaxRollDir = 1f;

    public bool IsMaxRolling => m_fMaxRollElapsed >= 0f;

    [Header("Hit Shake")]
    [SerializeField] private float m_fHitShakeDuration = 0.35f;
    [SerializeField] private float m_fHitShakeFrequency = 30f;
    [SerializeField] private float m_fHitRollKick = 6f;
    [SerializeField] private float m_fHitPitchKick = 4f;

    private float m_fShakeElapsed = -1f; // 0 미만이면 비활성화
    private float m_fShakeRollKick;
    private float m_fShakePitchKick;

    private void OnEnable()
    {
        m_vRollDir = Vector3.zero;
    }

    private void LateUpdate()
    {
        Quaternion qRoll;

        if (m_fMaxRollElapsed >= 0f)
        {
            m_fMaxRollElapsed += Time.deltaTime;
            float fRatio = Mathf.Clamp01(m_fMaxRollElapsed / m_fMaxRollDuration);
            float fSpinAngle = fRatio * 360f * m_fMaxRollDir;

            qRoll = Quaternion.Euler(0.0f, 0.0f, -fSpinAngle);
            m_fCurrentRollZ = 0f; // 롤 종료 후 뱅킹 각도로 자연스럽게 이어지도록 초기화

            if (fRatio >= 1.0f)
                m_fMaxRollElapsed = -1f;
        }
        else
        {
            float fZ = m_vRollDir.z;
            float fX = m_vRollDir.x;

            float fTargetRollZ = fZ * m_fMaxRollAngle;
            float fTargetRollX = fX * m_fMaxRollAngle;

            m_fCurrentRollZ = Mathf.Lerp(m_fCurrentRollZ, fTargetRollZ, m_fRollSpeed * Time.deltaTime);
            m_fCurrentRollX = Mathf.Lerp(m_fCurrentRollX, fTargetRollX, m_fRollSpeed * Time.deltaTime);
            qRoll = Quaternion.Euler(0.0f, m_fCurrentRollX, -m_fCurrentRollZ);
        }

        Quaternion qShake = UpdateHitShake();

        transform.localRotation = qRoll * qShake;
    }

    // MaxRoll(배럴롤) 연출 시작: _fDir 부호로 회전 방향을, _fDuration으로 360도 회전에 걸리는 시간을 결정
    public void PlayMaxRoll(float _fDir, float _fDuration)
    {
        m_fMaxRollDir = Mathf.Sign(_fDir);
        m_fMaxRollDuration = Mathf.Max(_fDuration, 0.0001f);
        m_fMaxRollElapsed = 0f;
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
