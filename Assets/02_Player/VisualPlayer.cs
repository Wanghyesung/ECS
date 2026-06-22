using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                VisualPlayer
기능 : 플레이어의 직접적인 로직이 아닌, 플레이어의 보이는 모습을 결정
 *///////////////////////////////////////////


public class VisualPlayer : MonoBehaviour
{
    [SerializeField] private float m_fMaxRollAngle = 45f;
    [SerializeField] private float m_fRollSpeed = 5f;
    private float m_fCurrentRoll;

    private void Update()
    {
        float fX = InputManager.m_Instance.InputInfo.MoveDir.x;
        float fTargetRoll = fX * m_fMaxRollAngle;

        m_fCurrentRoll = Mathf.Lerp(m_fCurrentRoll, fTargetRoll, m_fRollSpeed * Time.deltaTime);
        Quaternion qRoll = Quaternion.Euler(0.0f, 0.0f, -m_fCurrentRoll);
        transform.localRotation = qRoll;
    }
}
