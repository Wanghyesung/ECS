using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PlayerMovement : MonoBehaviour
{

    private Rigidbody m_refRigidbody = null;
    private Animator m_refAnimator = null;
    private Player m_refOwner = null;

    private Vector2 m_vInput;
    private Vector2 m_vMove;
    private Vector3 m_vLookDir;

    //private bool m_bMove;
    
    private const float MOVE_OFFSET = 10.0f;
    [SerializeField] private float m_fAngleSpeed = 12.0f;
    [SerializeField] private float m_fMoveSpeed = 5.0f;

    private void Awake()
    {
        m_refRigidbody = GetComponent<Rigidbody>();
        m_refAnimator = GetComponent<Animator>();
        m_refOwner = GetComponent<Player>();
    }


    private void Update()
    {
        m_vInput.x = Input.GetAxisRaw("Horizontal");
        m_vInput.y = Input.GetAxisRaw("Vertical");
        if (m_vInput.sqrMagnitude <= 0.01f)
        {
            m_vInput = Vector2.zero;
            m_vLookDir = Vector3.zero;

            //Todo : 이것도 나중에 애니메이션 테이블로 바꾸기 enum으로 접근해서
            m_refOwner.UpdateOnAnimation(eEntityState.Move, false);

        }
        else
        {
            m_vLookDir = new Vector3(m_vInput.x, 0.0f, m_vInput.y).normalized;
            m_refOwner.UpdateOnAnimation(eEntityState.Move, true);
        }
    }   

    private void FixedUpdate()
    {
        Vector3 vMove = new Vector3(m_vInput.x, 0.0f, m_vInput.y);
        if (vMove.sqrMagnitude > 1f)
            vMove = vMove.normalized;   

        Vector3 vNewPos = m_refRigidbody.position + vMove * m_fMoveSpeed * Time.fixedDeltaTime;
        m_refRigidbody.MovePosition(vNewPos);

        if (m_vLookDir.sqrMagnitude > 0.001f)
        {
            Quaternion m_qTarget = Quaternion.LookRotation(m_vLookDir);
            Quaternion m_qNext = Quaternion.Slerp(m_refRigidbody.rotation, m_qTarget, m_fAngleSpeed * Time.fixedDeltaTime);
            m_refRigidbody.MoveRotation(m_qNext);
        }
    }

}
