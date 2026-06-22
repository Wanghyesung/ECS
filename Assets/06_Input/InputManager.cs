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
    public Vector2 MoveDir = Vector2.zero; //X, Z
    public Vector2 ScreenPos = Vector2.zero; //X Y
    public Vector2 Delta = Vector2.zero;
}

public class InputManager : MonoBehaviour
{
    private InputInfo m_refInputInfo = new InputInfo();
    public InputInfo InputInfo => m_refInputInfo;

    public static InputManager m_Instance = null;

    [SerializeField] private List<InputActionReference> m_listMoveAction;
    [SerializeField] private List<InputActionReference> m_listScreenAction;
    [SerializeField] private List<InputActionReference> m_listDeltaAction;

    private bool m_isDeltaInitialized = false;
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

        for (int i = 0; i < m_listDeltaAction.Count; ++i)
            m_listDeltaAction[i].action.Enable();

    }

    private void Start()
    {

    }

    private void Update()
    {
        UpdateMoveValue();

        UpdateScreenMoveValue();

        UpdateDeltaValue();

    }

    private void UpdateMoveValue()
    {
        for (int i = 0; i < m_listMoveAction.Count; ++i)
        {
            Vector2 vMoveValue = m_listMoveAction[i].action.ReadValue<Vector2>();
            m_refInputInfo.MoveDir = vMoveValue.normalized;
        }
    }

    private void UpdateScreenMoveValue()
    {
        for (int i = 0; i < m_listScreenAction.Count; ++i)
        {
            Vector2 vScreenPos = m_listScreenAction[i].action.ReadValue<Vector2>();
            m_refInputInfo.ScreenPos = vScreenPos;
        }
    }

    private void UpdateDeltaValue()
    {
        for (int i = 0; i < m_listDeltaAction.Count; ++i)
        {
            Vector2 vDelta = m_listDeltaAction[i].action.ReadValue<Vector2>();

            if (!m_isDeltaInitialized)
            {
                if (vDelta.sqrMagnitude > 0f)
                {
                    m_refInputInfo.Delta = Vector2.zero; // 튀는 첫 값은 강제로 0 처리
                    m_isDeltaInitialized = true;        // 다음 프레임부터는 정상 작동
                    continue;
                }
            }

            m_refInputInfo.Delta = vDelta;
        }

    }
}