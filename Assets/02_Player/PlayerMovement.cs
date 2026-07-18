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

    private float m_fBoostValue = 1.0f;
    private float m_fBoostDecay = 3.0f; // 초당 배율 감소량
    private float m_fBostDir = 0.0f;

    // MaxRoll 등에서 순간적으로 이동속도를 올렸다가 서서히 원래 속도로 되돌릴 때 사용
    public void ApplySpeedBoost(float _fRollDir, float _fBoostMultiplier, float _fDecayPerSecond)
    {
        m_fBoostValue = Mathf.Max(m_fBoostValue, _fBoostMultiplier);
        m_fBoostDecay = _fDecayPerSecond;

        m_fBostDir = _fRollDir;
    }

    // Player.UpSpeed()에서 레벨업 시점에 호출. m_fMoveSpeed를 한 번만 늘려주면
    // FixedUpdate가 매 프레임 참조하는 값이라 재계산 없이 바로 반영됨
    public void AddMoveSpeed(float _fValue)
    {
        m_fMoveSpeed += _fValue;
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

        if (m_fBoostValue > 1.0f)
            m_fBoostValue = Mathf.Max(1.0f, m_fBoostValue - m_fBoostDecay * Time.deltaTime);
    }

    private void FixedUpdate()
    {
        // 부스트 종료 시(m_fBoostValue == 1.0f) 가산 속도가 정확히 0이 되도록 -1.0f를 뺀 값을 사용
        Vector3 vBoostSpeed = transform.right * m_fBostDir * (m_fBoostValue - 1.0f);

        Vector3 vMove = transform.forward * m_vInput.y + transform.right * m_vInput.x;

        Vector3 vNewPos = m_refRigidbody.position + (vMove * m_fMoveSpeed * m_fBoostValue + vBoostSpeed * m_fMoveSpeed) * Time.fixedDeltaTime;

        m_refRigidbody.MovePosition(vNewPos);
    }

}
