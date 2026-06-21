using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;


/*///////////////////////////////////////////
                InputManager
기능 : 연결된 액션의 값을 가져와서 해당 값 셋팅
 *///////////////////////////////////////////

public class InputInfo
{
    public Vector2 MoveDir; //X, Z
    public Vector2 ScreenMoveDir; //X Y
}

public class InputManager : MonoBehaviour
{
    private InputInfo m_refInputInfo = new InputInfo();
    public InputInfo InputInfo => m_refInputInfo;

    public static InputManager m_Instance = null;

    [SerializeField] private List<InputActionReference> m_listMoveAction;
    [SerializeField] private List<InputActionReference> m_listScreenAction;

    private Vector2 m_vStartPosition = Vector2.zero;
    private Vector2 m_vCurPosition = Vector2.zero;

    private void Awake()
    {
        if (m_Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < m_listMoveAction.Count; ++i)
            m_listMoveAction[i].action.Enable();

        for (int i = 0; i < m_listScreenAction.Count; ++i)
            m_listScreenAction[i].action.Enable();
    }


    private void Update()
    {
        UpdateMoveValue();

        UpdateScreenMoveValue();
    }

    private void UpdateMoveValue()
    {
        for(int i = 0; i<m_listMoveAction.Count; ++i)
        {
            Vector2 vMoveValue = m_listMoveAction[i].action.ReadValue<Vector2>();
            m_refInputInfo.MoveDir = vMoveValue.normalized;
        }
    }

    private void UpdateScreenMoveValue()
    {
        for (int i = 0; i < m_listScreenAction.Count; ++i)
        {
            Vector2 vScreenMoveValue = m_listScreenAction[i].action.ReadValue<Vector2>();

            m_vCurPosition = vScreenMoveValue;

            if (vScreenMoveValue == Vector2.zero)
                m_vStartPosition = m_vCurPosition;
          
            Vector2 vDrag = m_vCurPosition - m_vStartPosition;
            m_refInputInfo.ScreenMoveDir = vDrag.normalized;
                
        }
    }

}
