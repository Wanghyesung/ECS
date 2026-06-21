using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

public class PlayerMovement : MonoBehaviour
{

    private Rigidbody m_refRigidbody = null;
    private Animator m_refAnimator = null;
    private Player m_refOwner = null;

    private Vector2 m_vInput;
    private Vector2 m_vMove;
    private Vector3 m_vLookDir;

    //private bool m_bMove;
    
    [SerializeField] private float m_fMoveSpeed = 5.0f;

    [SerializeField] private float m_fMaxRollAngle = 45f;
    [SerializeField] private float m_fRollSpeed = 5f;
    private float m_fCurrentRoll;
    private float m_fTargetRoll;

    //[SerializeField] private float m_fAngleSpeed = 12.0f;

    private void Awake()
    {
        m_refRigidbody = GetComponent<Rigidbody>();
        m_refAnimator = GetComponent<Animator>();
        m_refOwner = GetComponent<Player>();
    }


    private void Update()
    {
        m_vInput = InputManager.m_Instance.InputInfo.MoveDir;

        // Debug.Log(m_vInput);
        // Debug.Log(m_vInput.sqrMagnitude);

        if (m_vInput.sqrMagnitude <= 0.001f)
        {
            m_vInput = Vector2.zero;
            m_vLookDir = Vector3.zero;

            //m_refOwner.UpdateOnAnimation(eEntityState.Move, false);
        }
        else
        {
            m_vLookDir = new Vector3(m_vInput.x, 0.0f, m_vInput.y).normalized;
            //m_refOwner.UpdateOnAnimation(eEntityState.Move, true);
        }

        float fTargetRoll = m_vInput.x * m_fMaxRollAngle;
        m_fCurrentRoll = Mathf.Lerp(
            m_fCurrentRoll,
            fTargetRoll,
            m_fRollSpeed * Time.deltaTime
        );

        transform.localRotation = Quaternion.Euler(0f, 0f, -m_fCurrentRoll);
    }   

    private void FixedUpdate()
    {
        Vector3 vMove = transform.forward * m_vInput.y + transform.right * m_vInput.x;
        if (vMove.sqrMagnitude > 1f)
            vMove = vMove.normalized;   

        Vector3 vNewPos = m_refRigidbody.position + vMove * m_fMoveSpeed * Time.fixedDeltaTime;
        m_refRigidbody.MovePosition(vNewPos);

        //if (m_vLookDir.sqrMagnitude > 0.001f)
        //{
        //    Quaternion m_qTarget = Quaternion.LookRotation(m_vLookDir);
        //    Quaternion m_qNext = Quaternion.Slerp(m_refRigidbody.rotation, m_qTarget, m_fAngleSpeed * Time.fixedDeltaTime);
        //    m_refRigidbody.MoveRotation(m_qNext);
        //}
    }

}
