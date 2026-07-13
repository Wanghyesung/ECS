using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Rendering.LookDev;
using UnityEngine;
using UnityEngine.AI;
using static UnityEditor.Searcher.SearcherWindow.Alignment;



/*///////////////////////////////////////////
                PlayerMovement
기능 : 플레이어의 실질적인 움직임을 담당하는 기능
*////////////////////////////////////////////

public class PlayerMovement : MonoBehaviour
{

    [SerializeField] private VisualObject m_refVSObject = null;
    private Rigidbody m_refRigidbody = null;
    private Player m_refOwner = null;
   

    private Vector2 m_vInput;
    private Vector3 m_vRotate = Vector3.zero;
    private Vector2 m_vDelta;
   
    
    [SerializeField] private float m_fMoveSpeed = 5.0f;
    [SerializeField] private float m_fAngleSpeed = 12.0f;

    private float m_fSpeedBoostMultiplier = 1.0f;
    private float m_fSpeedBoostDecay = 3.0f; // 초당 배율 감소량

    // MaxRoll 등에서 순간적으로 이동속도를 올렸다가 서서히 원래 속도로 되돌릴 때 사용
    public void ApplySpeedBoost(float _fBoostMultiplier, float _fDecayPerSecond)
    {
        m_fSpeedBoostMultiplier = Mathf.Max(m_fSpeedBoostMultiplier, _fBoostMultiplier);
        m_fSpeedBoostDecay = _fDecayPerSecond;
    }
    private void Awake()
    {
        m_refRigidbody = GetComponent<Rigidbody>();
        m_refOwner = GetComponent<Player>();
    }


    private void Update()
    {
        m_vInput = InputManager.m_Instance.InputInfo.MoveDir;
        m_vDelta = InputManager.m_Instance.InputInfo.Delta;

        m_refVSObject.RollZ = m_vInput.x;

        float fY = m_vDelta.x * m_fAngleSpeed * 3 * Time.deltaTime;
        float fX = m_vDelta.y * m_fAngleSpeed  * Time.deltaTime * -1;
     
        m_vRotate.y += fY;
        m_vRotate.x += fX;

        m_vRotate.x = Mathf.Clamp(m_vRotate.x, -85.0f, 40.0f);

        transform.rotation = Quaternion.Euler(m_vRotate.x, m_vRotate.y, 0.0f);

        if (m_fSpeedBoostMultiplier > 1.0f)
            m_fSpeedBoostMultiplier = Mathf.Max(1.0f, m_fSpeedBoostMultiplier - m_fSpeedBoostDecay * Time.deltaTime);
    }

    private void FixedUpdate()
    {
        Vector3 vMove = transform.forward * m_vInput.y + transform.right * m_vInput.x;
        Vector3 vNewPos = m_refRigidbody.position + vMove * m_fMoveSpeed * m_fSpeedBoostMultiplier * Time.fixedDeltaTime;
        m_refRigidbody.MovePosition(vNewPos);;
    }

}
