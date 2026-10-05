---
name: input-system
description: "New Input System — 액션 맵, PlayerInput 컴포넌트, 자동 생성 C# 클래스, 런타임 리바인딩, 멀티 디바이스 지원, 입력 버퍼링. 입력 처리 작업에 사용합니다."
globs: ["**/*.inputactions", "**/Input*.cs", "**/PlayerInput*"]
---

# Unity New Input System

## Input Action Asset 설정

Input Action Asset을 생성하세요: Assets > Create > Input Actions. 이 에셋이 모든 입력 바인딩의 중앙 설정 파일이 됩니다.

### Action Map 구조

컨텍스트에 따라 액션을 맵으로 구성하세요.

```
PlayerControls.inputactions
  |-- Player (게임플레이)
  |     |-- Move (Value, Vector2)
  |     |-- Look (Value, Vector2)
  |     |-- Jump (Button)
  |     |-- Attack (Button)
  |     |-- Interact (Button)
  |
  |-- UI (메뉴 내비게이션)
  |     |-- Navigate (Value, Vector2)
  |     |-- Submit (Button)
  |     |-- Cancel (Button)
  |
  |-- Menu (일시정지/설정)
        |-- Pause (Button)
```

### Action 타입

- **Button**: 이산적인 누름/뗌. 점프, 공격, 상호작용에 사용합니다.
- **Value**: 연속적인 값. 이동, 시점 조작, 트리거에 사용합니다.
- **Pass-Through**: Value와 비슷하지만 초기 상태 체크를 하지 않습니다. 멀티 디바이스 시나리오에 사용합니다.

## 자동 생성된 C# 클래스 워크플로우

Input Action Asset 인스펙터에서 "Generate C# Class"를 체크하고 Apply를 클릭하세요. 이 방식을 권장합니다.

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerController : MonoBehaviour
{
    private PlayerControls m_controls;
    private Vector2 m_vMoveInput;

    private void Awake()
    {
        m_controls = new PlayerControls();
    }

    private void OnEnable()
    {
        m_controls.Player.Enable();

        m_controls.Player.Move.performed += OnMove;
        m_controls.Player.Move.canceled += OnMove;
        m_controls.Player.Jump.performed += OnJump;
        m_controls.Player.Attack.performed += OnAttack;
    }

    private void OnDisable()
    {
        m_controls.Player.Move.performed -= OnMove;
        m_controls.Player.Move.canceled -= OnMove;
        m_controls.Player.Jump.performed -= OnJump;
        m_controls.Player.Attack.performed -= OnAttack;

        m_controls.Player.Disable();
    }

    private void OnMove(InputAction.CallbackContext _ctx)
    {
        m_vMoveInput = _ctx.ReadValue<Vector2>();
    }

    private void OnJump(InputAction.CallbackContext _ctx)
    {
        // 점프 로직
    }

    private void OnAttack(InputAction.CallbackContext _ctx)
    {
        // 공격 로직
    }

    private void Update()
    {
        transform.Translate(new Vector3(m_vMoveInput.x, 0, m_vMoveInput.y) * Time.deltaTime * 5f);
    }
}
```

## PlayerInput 컴포넌트

PlayerInput 컴포넌트는 더 쉽지만 유연성은 떨어지는 방식을 제공합니다.

### 동작 모드

| 모드 | 장점 | 단점 |
|------|------|------|
| Send Messages | 간단, 별도 설정 불필요 | SendMessage 사용 (느림, 타입 안전성 없음) |
| Broadcast Messages | 자식 오브젝트까지 전달 | SendMessages와 동일한 문제 |
| Invoke Unity Events | 인스펙터에서 할당, 유연함 | 인스펙터에서 배선 작업 필요 |
| Invoke C# Events | 최고 성능, 타입 안전 | 코드에서 직접 구독 필요 |

### Invoke C# Events 사용하기

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public sealed class PlayerInputHandler : MonoBehaviour
{
    private PlayerInput m_refPlayerInput;

    private void Awake()
    {
        m_refPlayerInput = GetComponent<PlayerInput>();
    }

    private void OnEnable()
    {
        m_refPlayerInput.onActionTriggered += OnActionTriggered;
    }

    private void OnDisable()
    {
        m_refPlayerInput.onActionTriggered -= OnActionTriggered;
    }

    private void OnActionTriggered(InputAction.CallbackContext _ctx)
    {
        switch (_ctx.action.name)
        {
            case "Move":
                HandleMove(_ctx.ReadValue<Vector2>());
                break;
            case "Jump":
                if (_ctx.performed) HandleJump();
                break;
        }
    }

    private void HandleMove(Vector2 _vInput) { /* ... */ }
    private void HandleJump() { /* ... */ }
}
```

## 입력 값 읽기

### 콜백 단계

```csharp
refAction.started += _ctx => { };   // 입력 시작됨 (버튼을 누르기 시작)
refAction.performed += _ctx => { }; // 입력 완료됨 (버튼이 완전히 눌림)
refAction.canceled += _ctx => { };  // 입력이 해제됨
```

### Update에서 폴링하기 (대안)

```csharp
private void Update()
{
    // 폴링 방식 — 더 단순하지만 이벤트 기반보다 덜 반응적임
    Vector2 vMove = m_controls.Player.Move.ReadValue<Vector2>();
    bool bJumpPressed = m_controls.Player.Jump.WasPressedThisFrame();
    bool bJumpReleased = m_controls.Player.Jump.WasReleasedThisFrame();
    bool bJumpHeld = m_controls.Player.Jump.IsPressed();
}
```

## Action Map 전환

```csharp
public sealed class InputMapSwitcher : MonoBehaviour
{
    private PlayerControls m_controls;

    public void SwitchToUI()
    {
        m_controls.Player.Disable();
        m_controls.UI.Enable();
    }

    public void SwitchToGameplay()
    {
        m_controls.UI.Disable();
        m_controls.Player.Enable();
    }

    public void SwitchToMenu()
    {
        m_controls.Player.Disable();
        m_controls.UI.Disable();
        m_controls.Menu.Enable();
    }
}
```

## Composite Binding

### 2D Vector Composite (WASD / 방향패드)

Input Action Asset에서 Vector2 액션에 2D Vector Composite를 추가하세요:
- Up: W / 방향패드 위
- Down: S / 방향패드 아래
- Left: A / 방향패드 왼쪽
- Right: D / 방향패드 오른쪽

### Button With Modifier

Ctrl+S 같은 키 조합의 경우:
- ButtonWithOneModifier Composite 추가
- Modifier: Left Ctrl
- Button: S

## 런타임 리바인딩

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public sealed class RebindManager : MonoBehaviour
{
    [SerializeField] private InputActionReference m_refActionToRebind;
    [SerializeField] private TMP_Text m_refBindingDisplayText;
    [SerializeField] private GameObject m_refWaitingForInputUI;

    private InputActionRebindingExtensions.RebindingOperation m_rebindOperation;

    public void StartRebinding()
    {
        m_refActionToRebind.action.Disable();
        m_refWaitingForInputUI.SetActive(true);

        m_rebindOperation = m_refActionToRebind.action.PerformInteractiveRebinding()
            .WithControlsExcluding("Mouse") // 마우스 이동 제외
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f) // 디바운스
            .OnComplete(_operation => RebindComplete())
            .OnCancel(_operation => RebindCanceled())
            .Start();
    }

    private void RebindComplete()
    {
        m_rebindOperation.Dispose();
        m_rebindOperation = null;
        m_refWaitingForInputUI.SetActive(false);

        m_refActionToRebind.action.Enable();
        UpdateBindingDisplay();

        // 바인딩 오버라이드 저장
        string strRebinds = m_refActionToRebind.action.actionMap.asset.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString("InputBindings", strRebinds);
    }

    private void RebindCanceled()
    {
        m_rebindOperation.Dispose();
        m_rebindOperation = null;
        m_refWaitingForInputUI.SetActive(false);
        m_refActionToRebind.action.Enable();
    }

    private void UpdateBindingDisplay()
    {
        int iBindingIndex = m_refActionToRebind.action.GetBindingIndexForControl(
            m_refActionToRebind.action.controls[0]);
        m_refBindingDisplayText.text = InputControlPath.ToHumanReadableString(
            m_refActionToRebind.action.bindings[iBindingIndex].effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice);
    }

    public void LoadSavedBindings()
    {
        string strRebinds = PlayerPrefs.GetString("InputBindings", string.Empty);
        if (!string.IsNullOrEmpty(strRebinds))
        {
            m_refActionToRebind.action.actionMap.asset.LoadBindingOverridesFromJson(strRebinds);
        }
    }
}
```

## 입력 버퍼링 패턴

착지 직전에 눌러도 점프 같은 액션이 인식되도록 입력을 버퍼링합니다.

```csharp
public sealed class InputBuffer : MonoBehaviour
{
    [SerializeField] private float m_fBufferDuration = 0.15f;

    private float m_fJumpBufferTimer;
    private bool m_bJumpConsumed;

    private void Update()
    {
        if (m_fJumpBufferTimer > 0f)
        {
            m_fJumpBufferTimer -= Time.deltaTime;
        }
    }

    // 입력 콜백에서 호출됨
    public void OnJumpPressed()
    {
        m_fJumpBufferTimer = m_fBufferDuration;
        m_bJumpConsumed = false;
    }

    // 점프가 가능한 시점에 이동/물리 코드에서 호출됨
    public bool ConsumeJumpBuffer()
    {
        if (m_fJumpBufferTimer > 0f && !m_bJumpConsumed)
        {
            m_bJumpConsumed = true;
            m_fJumpBufferTimer = 0f;
            return true;
        }
        return false;
    }
}
```

## 디바이스 감지

```csharp
using UnityEngine.InputSystem;

public sealed class DeviceDetector : MonoBehaviour
{
    public enum InputScheme { KeyboardMouse, Gamepad, Touch }
    public InputScheme CurrentScheme { get; private set; }

    public event System.Action<InputScheme> OnSchemeChanged;

    private void OnEnable()
    {
        InputSystem.onActionChange += OnActionChange;
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= OnActionChange;
    }

    private void OnActionChange(object _refObj, InputActionChange _eChange)
    {
        if (_eChange != InputActionChange.ActionPerformed) return;

        var refAction = (InputAction)_refObj;
        var refDevice = refAction.activeControl?.device;

        InputScheme eNewScheme = refDevice switch
        {
            Keyboard or Mouse => InputScheme.KeyboardMouse,
            Gamepad => InputScheme.Gamepad,
            Touchscreen => InputScheme.Touch,
            _ => CurrentScheme
        };

        if (eNewScheme != CurrentScheme)
        {
            CurrentScheme = eNewScheme;
            OnSchemeChanged?.Invoke(CurrentScheme);
        }
    }
}
```

## 멀티플레이어 입력 (PlayerInputManager)

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class LocalMultiplayerManager : MonoBehaviour
{
    [SerializeField] private PlayerInputManager m_refPlayerInputManager;

    private void OnEnable()
    {
        m_refPlayerInputManager.onPlayerJoined += OnPlayerJoined;
        m_refPlayerInputManager.onPlayerLeft += OnPlayerLeft;
    }

    private void OnDisable()
    {
        m_refPlayerInputManager.onPlayerJoined -= OnPlayerJoined;
        m_refPlayerInputManager.onPlayerLeft -= OnPlayerLeft;
    }

    private void OnPlayerJoined(PlayerInput _refPlayerInput)
    {
        Debug.Log($"Player {_refPlayerInput.playerIndex} joined with {_refPlayerInput.currentControlScheme}");
    }

    private void OnPlayerLeft(PlayerInput _refPlayerInput)
    {
        Debug.Log($"Player {_refPlayerInput.playerIndex} left");
    }
}
```

## 모바일 터치 입력

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public sealed class TouchInputHandler : MonoBehaviour
{
    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        foreach (var refTouch in Touch.activeTouches)
        {
            switch (refTouch.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    HandleTouchStart(refTouch.screenPosition);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Moved:
                    HandleTouchMove(refTouch.screenPosition, refTouch.delta);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                    HandleTouchEnd(refTouch.screenPosition);
                    break;
            }
        }
    }

    private void HandleTouchStart(Vector2 _vPos) { /* ... */ }
    private void HandleTouchMove(Vector2 _vPos, Vector2 _vDelta) { /* ... */ }
    private void HandleTouchEnd(Vector2 _vPos) { /* ... */ }
}
```
