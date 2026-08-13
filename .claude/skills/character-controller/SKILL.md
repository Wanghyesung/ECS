---
name: character-controller
description: "2D 및 3D 캐릭터 컨트롤러 패턴 — 코요테 타임, 입력 버퍼링, 가변 점프, 벽 슬라이드/점프, 대시, 경사면, 계단, 카메라 상대 이동. 플레이어 이동 구현 시 로드할 것."
globs: ["**/Player*.cs", "**/Character*.cs", "**/Movement*.cs", "**/Controller*.cs"]
---

# 캐릭터 컨트롤러 패턴

Unity에서 반응성이 좋고 게임 필(feel)이 잘 다듬어진 캐릭터 컨트롤러를 만들기 위한 종합 레퍼런스. 2D 플랫포머와 3D 액션 게임 패턴을 모두 다룬다.

## 2D 캐릭터 컨트롤러

### 지면 감지 (Ground Detection)

충돌 콜백에 의존하기보다는 캐릭터 발밑에서 overlap circle을 사용하라. 이렇게 하면 프레임 단위로 정확한 지면 상태를 얻을 수 있다.

```csharp
[Header("Ground Check")]
[SerializeField] private Transform m_refGroundCheckPoint;
[SerializeField] private float m_fGroundCheckRadius = 0.15f;
[SerializeField] private LayerMask m_groundLayer;

private bool m_bIsGrounded;

private void CheckGround()
{
    m_bIsGrounded = Physics2D.OverlapCircle(
        m_refGroundCheckPoint.position,
        m_fGroundCheckRadius,
        m_groundLayer
    );
}
```

`m_refGroundCheckPoint`는 캐릭터 스프라이트 하단에 위치한 자식 트랜스폼으로 배치하라. 벽에서의 오탐(false positive)을 피하려면 반지름을 작게 유지해야 한다.

### 코요테 타임 (Coyote Time)

플레이어가 발판에서 떨어진 직후 잠깐의 유예 시간 동안 점프를 허용한다. 약간의 타이밍 실수를 눈감아 주어 플랫포밍을 가혹하지 않고 관대하게 느껴지도록 만든다.

```csharp
[Header("Coyote Time")]
[SerializeField] private float m_fCoyoteTimeDuration = 0.1f;

private float m_fCoyoteTimeCounter;

private void Update()
{
    if (m_bIsGrounded)
    {
        m_fCoyoteTimeCounter = m_fCoyoteTimeDuration;
    }
    else
    {
        m_fCoyoteTimeCounter -= Time.deltaTime;
    }

    if (m_bJumpPressed && m_fCoyoteTimeCounter > 0f)
    {
        ExecuteJump();
        m_fCoyoteTimeCounter = 0f; // 코요테 타임 소진
    }
}
```

일반적인 값은 0.08 ~ 0.15초다. 값이 클수록 더 관대하게 느껴지고, 작을수록 더 빡빡하게 느껴진다. 게임의 템포에 맞는 최적점을 찾으려면 실제로 플레이해 보면서 조정해야 한다.

### 입력 버퍼링 (Input Buffering)

플레이어가 버튼을 몇 프레임 일찍 눌렀더라도, 착지하는 순간 점프가 발동되도록 입력을 큐에 담아둔다. 코요테 타임과 결합하면 "분명히 점프를 눌렀는데 아무 반응이 없었다"는 불만 대부분을 없앨 수 있다.

```csharp
[Header("Input Buffering")]
[SerializeField] private float m_fJumpBufferDuration = 0.12f;

private float m_fJumpBufferCounter;

private void Update()
{
    // 입력을 버퍼에 담는다
    if (m_bJumpPressedThisFrame)
    {
        m_fJumpBufferCounter = m_fJumpBufferDuration;
    }
    else
    {
        m_fJumpBufferCounter -= Time.deltaTime;
    }

    // 접지 상태(또는 코요테 타임 중)일 때 버퍼를 소진한다
    if (m_fJumpBufferCounter > 0f && m_fCoyoteTimeCounter > 0f)
    {
        ExecuteJump();
        m_fJumpBufferCounter = 0f;
        m_fCoyoteTimeCounter = 0f;
    }
}
```

### 가변 점프 높이 (Variable Jump Height)

플레이어가 버튼을 일찍 놓으면 점프를 짧게 끊는다. 이를 통해 플레이어가 점프 궤적의 높이를 세밀하게 제어할 수 있다.

```csharp
[Header("Variable Jump")]
[SerializeField] private float m_fJumpForce = 14f;
[SerializeField] private float m_fJumpCutMultiplier = 0.4f;

private Rigidbody2D m_refRigidbody2D;

private void ExecuteJump()
{
    // 일관된 점프 높이를 위해 힘을 가하기 전에 수직 속도를 초기화한다
    m_refRigidbody2D.velocity = new Vector2(m_refRigidbody2D.velocity.x, 0f);
    m_refRigidbody2D.AddForce(Vector2.up * m_fJumpForce, ForceMode2D.Impulse);
}

private void Update()
{
    // 플레이어가 위로 이동 중일 때 점프 버튼을 놓으면 속도를 잘라낸다
    if (m_bJumpReleasedThisFrame && m_refRigidbody2D.velocity.y > 0f)
    {
        m_refRigidbody2D.velocity = new Vector2(
            m_refRigidbody2D.velocity.x,
            m_refRigidbody2D.velocity.y * m_fJumpCutMultiplier
        );
    }
}
```

`m_fJumpCutMultiplier`는 얼마만큼의 속도를 유지할지를 결정한다. 값이 0.4라면 일찍 놓았을 때 최대 점프 높이의 약 40%가 나온다는 뜻이다. 이 값은 중력 스케일과 함께 튜닝해야 한다.

### 벽 슬라이드와 벽 점프 (Wall Slide and Wall Jump)

수평 레이캐스트나 박스 overlap으로 벽을 감지한다. 슬라이드 중에는 중력을 줄여서 적용하고, 점프 시 벽 반대 방향으로 밀어낸다.

```csharp
[Header("Wall Interaction")]
[SerializeField] private Transform m_refWallCheckPoint;
[SerializeField] private float m_fWallCheckDistance = 0.3f;
[SerializeField] private LayerMask m_wallLayer;
[SerializeField] private float m_fWallSlideSpeed = 2f;
[SerializeField] private Vector2 m_vWallJumpForce = new Vector2(12f, 16f);
[SerializeField] private float m_fWallJumpLockTime = 0.15f;

private bool m_bIsTouchingWall;
private bool m_bIsWallSliding;
private float m_fWallJumpLockCounter;
private int m_iWallDirection; // -1 왼쪽, 1 오른쪽

private void CheckWall()
{
    m_bIsTouchingWall = Physics2D.Raycast(
        m_refWallCheckPoint.position,
        Vector2.right * transform.localScale.x,
        m_fWallCheckDistance,
        m_wallLayer
    );

    // 공중에 떠 있고, 벽에 닿아 있으며, 벽 쪽으로 입력 중일 때 벽 슬라이드
    m_bIsWallSliding = m_bIsTouchingWall && !m_bIsGrounded && m_vMoveInput.x != 0f;
}

private void ApplyWallSlide()
{
    if (!m_bIsWallSliding) return;

    // 하강 속도를 슬라이드 속도로 클램프한다
    if (m_refRigidbody2D.velocity.y < -m_fWallSlideSpeed)
    {
        m_refRigidbody2D.velocity = new Vector2(m_refRigidbody2D.velocity.x, -m_fWallSlideSpeed);
    }

    m_iWallDirection = transform.localScale.x > 0 ? 1 : -1;
}

private void WallJump()
{
    if (!m_bIsWallSliding) return;

    // 벽 반대 방향으로 점프
    m_refRigidbody2D.velocity = Vector2.zero;
    m_refRigidbody2D.AddForce(new Vector2(-m_iWallDirection * m_vWallJumpForce.x, m_vWallJumpForce.y),
        ForceMode2D.Impulse);

    // 플레이어가 벽 쪽으로 다시 조작해 밀려 내려가지 않도록
    // 잠시 수평 입력을 잠근다
    m_fWallJumpLockCounter = m_fWallJumpLockTime;
}
```

벽 점프 이후의 입력 잠금은 매우 중요하다. 이게 없으면 벽 쪽을 계속 누르고 있는 플레이어는 수평 밀림을 즉시 상쇄시켜 바로 다시 미끄러져 내려온다.

### 대시 메커닉 (Dash Mechanic)

짧은 시간 동안 속도를 폭발적으로 올리며, 선택적으로 무적 프레임을 부여한다. 남용을 막기 위해 쿨다운을 사용한다.

```csharp
[Header("Dash")]
[SerializeField] private float m_fDashSpeed = 24f;
[SerializeField] private float m_fDashDuration = 0.12f;
[SerializeField] private float m_fDashCooldown = 0.6f;
[SerializeField] private bool m_bDashGrantsInvincibility = true;

private bool m_bIsDashing;
private float m_fDashCooldownCounter;

private IEnumerator DashCoroutine(Vector2 _vDirection)
{
    m_bIsDashing = true;
    m_fDashCooldownCounter = m_fDashCooldown;

    if (m_bDashGrantsInvincibility)
        Physics2D.IgnoreLayerCollision(m_playerLayer, m_enemyLayer, true);

    m_refRigidbody2D.gravityScale = 0f;
    m_refRigidbody2D.velocity = _vDirection.normalized * m_fDashSpeed;

    yield return new WaitForSeconds(m_fDashDuration);

    m_refRigidbody2D.gravityScale = m_fDefaultGravityScale;
    m_bIsDashing = false;

    if (m_bDashGrantsInvincibility)
        Physics2D.IgnoreLayerCollision(m_playerLayer, m_enemyLayer, false);
}
```

### 일방향 플랫폼 (One-Way Platforms)

통과 가능한 플랫폼에는 Unity의 `PlatformEffector2D` 컴포넌트를 사용하라. 플레이어는 아래에서 위로 점프해서 통과한 뒤 위에 설 수 있다.

- 플랫폼 GameObject에 `PlatformEffector2D`를 추가한다.
- `Surface Arc`를 180으로 설정한다 (윗면만 고체로 취급).
- 이펙터에서 `Use One Way`를 활성화한다.
- 플랫폼의 `Collider2D`에서 `Used By Effector`를 체크한다.
- 아래로 통과시키려면 잠시 콜라이더를 비활성화하거나, 이펙터의 `rotationalOffset`을 짧은 시간 동안 180으로 설정한다.

```csharp
private IEnumerator DropThroughPlatform(Collider2D _refPlatformCollider)
{
    _refPlatformCollider.enabled = false;
    yield return new WaitForSeconds(0.25f);
    _refPlatformCollider.enabled = true;
}
```

### 상태 머신 통합 (State Machine Integration)

중첩된 bool 변수 뭉치 대신 명시적인 상태로 이동 코드를 구성하라. 각 상태는 자신만의 enter/exit/update 로직을 가진다.

2D 플랫포머의 일반적인 플레이어 상태:
- **Idle** - 접지, 입력 없음
- **Run** - 접지, 수평 입력
- **Jump** - 점프 후 상승 중
- **Fall** - 공중, 하강 중
- **WallSlide** - 벽에 붙어 천천히 하강
- **Dash** - 대시 중, 중력 무시
- **Hurt** - 넉백당함, 잠시 입력 비활성화
- **Dead** - 입력 없음, 사망 애니메이션 재생

이런 상태들과 함께 사용할 수 있는 범용 FSM 구현은 `state-machine` 스킬을 참고하라.

---

## 3D 캐릭터 컨트롤러

### CharacterController vs Rigidbody

**CharacterController (내장):**
- 설정이 더 간단하며, `Move()`와 `SimpleMove()` 메서드를 제공
- `slopeLimit`과 `stepOffset`을 통한 경사면/계단 처리가 내장됨
- 기본적으로 물리 상호작용이 없음(아무것도 밀지 않고, 아무것도 나를 밀지 않음)
- 1인칭/3인칭 게임에서 완전한 제어권을 원할 때 적합

**Rigidbody 기반:**
- 물리 오브젝트와 자연스럽게 상호작용함
- 수동으로 지면 감지, 경사면 처리를 해야 함
- 떨림(jitter)을 피하려면 이동 처리를 반드시 `FixedUpdate`에서 해야 함
- 물리 상호작용이 많은 게임(상자 밀기, 움직이는 플랫폼 타기)에 적합

### 지면 감지 (3D)

캐릭터 하단에서 SphereCast를 쏘아 안정적으로 지면을 감지한다.

```csharp
[Header("Ground Check")]
[SerializeField] private float m_fGroundCheckDistance = 0.2f;
[SerializeField] private float m_fGroundCheckRadius = 0.3f;
[SerializeField] private LayerMask m_groundLayer;

private bool m_bIsGrounded;
private RaycastHit m_groundHit;

private void CheckGround()
{
    Vector3 vOrigin = transform.position + Vector3.up * m_fGroundCheckRadius;

    m_bIsGrounded = Physics.SphereCast(
        vOrigin,
        m_fGroundCheckRadius,
        Vector3.down,
        out m_groundHit,
        m_fGroundCheckDistance,
        m_groundLayer
    );
}
```

### 경사면 처리 (Slope Handling)

허용 가능한 경사면에서는 캐릭터가 미끄러지지 않도록 하고, 가파른 경사면에서는 강제로 미끄러지게 한다.

```csharp
[Header("Slopes")]
[SerializeField] private float m_fMaxSlopeAngle = 45f;
[SerializeField] private float m_fSlopeSlideSpeed = 8f;

private void HandleSlopes()
{
    if (!m_bIsGrounded) return;

    float fAngle = Vector3.Angle(Vector3.up, m_groundHit.normal);

    if (fAngle > m_fMaxSlopeAngle)
    {
        // 너무 가파름: 아래로 미끄러짐
        Vector3 vSlideDirection = Vector3.ProjectOnPlane(Vector3.down, m_groundHit.normal).normalized;
        m_refRigidbody.AddForce(vSlideDirection * m_fSlopeSlideSpeed, ForceMode.Acceleration);
    }
    else if (fAngle > 0f)
    {
        // 걸을 수 있는 경사면: 이동 방향을 경사면 표면에 투영한다
        // 이렇게 하면 내리막을 걸을 때 캐릭터가 튀어 오르지 않는다
        m_vMoveDirection = Vector3.ProjectOnPlane(m_vMoveDirection, m_groundHit.normal).normalized
                         * m_vMoveDirection.magnitude;
    }
}
```

### 계단 처리 (Stair Handling)

Rigidbody 기반 컨트롤러의 경우, 스텝 높이에서 전방 레이캐스트로 계단을 감지한 뒤 캐릭터를 위로 순간이동시킨다.

```csharp
[Header("Stairs")]
[SerializeField] private float m_fStepHeight = 0.35f;
[SerializeField] private float m_fStepCheckDepth = 0.4f;

private void HandleStairs()
{
    if (!m_bIsGrounded || m_vMoveDirection.magnitude < 0.01f) return;

    Vector3 vLowerOrigin = transform.position + Vector3.up * 0.05f;
    Vector3 vUpperOrigin = transform.position + Vector3.up * m_fStepHeight;

    // 발 높이에서 무언가에 막혀 있는지 확인
    bool bLowerBlocked = Physics.Raycast(vLowerOrigin, m_vMoveDirection.normalized,
        m_fStepCheckDepth, m_groundLayer);

    // 스텝 높이에서 공간이 비어 있는지 확인
    bool bUpperClear = !Physics.Raycast(vUpperOrigin, m_vMoveDirection.normalized,
        m_fStepCheckDepth, m_groundLayer);

    if (bLowerBlocked && bUpperClear)
    {
        transform.position += Vector3.up * m_fStepHeight;
    }
}
```

### 카메라 상대 이동 (Camera-Relative Movement)

원시 입력을 변환하여 "전방"이 월드 스페이스 전방이 아니라 "카메라가 바라보는 방향"이 되도록 한다.

```csharp
[SerializeField] private Transform m_refCameraTransform;

private Vector3 GetCameraRelativeMovement(Vector2 _vInput)
{
    Vector3 vCamForward = m_refCameraTransform.forward;
    Vector3 vCamRight = m_refCameraTransform.right;

    // 수평면으로 평탄화
    vCamForward.y = 0f;
    vCamRight.y = 0f;
    vCamForward.Normalize();
    vCamRight.Normalize();

    return vCamForward * _vInput.y + vCamRight * _vInput.x;
}
```

### 가변 낙하 속도를 적용한 중력 (Gravity with Variable Fall Speed)

낙하 중에는 더 강한 중력을 적용하여 더 경쾌하고 반응성 좋은 움직임을 만든다.

```csharp
[Header("Gravity")]
[SerializeField] private float m_fGravityScale = 2.5f;
[SerializeField] private float m_fFallMultiplier = 3.5f;
[SerializeField] private float m_fMaxFallSpeed = 30f;

private void ApplyGravity()
{
    float fMultiplier = m_refRigidbody.velocity.y < 0f ? m_fFallMultiplier : m_fGravityScale;
    Vector3 vGravity = Physics.gravity * (fMultiplier - 1f); // Unity가 이미 1배를 적용하므로 -1
    m_refRigidbody.AddForce(vGravity, ForceMode.Acceleration);

    // 낙하 속도 클램프
    if (m_refRigidbody.velocity.y < -m_fMaxFallSpeed)
    {
        m_refRigidbody.velocity = new Vector3(m_refRigidbody.velocity.x, -m_fMaxFallSpeed, m_refRigidbody.velocity.z);
    }
}
```

---

## 입력 시스템 통합

리바인딩 가능하고 멀티 디바이스를 지원하는 Unity의 새 Input System을 사용하라.

### 액션 맵 설정

일반적인 컨트롤러를 위해 다음 액션들로 Input Action Asset을 만든다:

| 액션      | 타입             | 바인딩 예시                        |
|-----------|-----------------|-----------------------------------|
| Move      | Value (Vector2) | WASD, 왼쪽 스틱                    |
| Jump      | Button          | Space, 하단 버튼(A/Cross)          |
| Dash      | Button          | Shift, 왼쪽 버튼(X/Square)         |
| Attack    | Button          | 마우스 왼쪽, 오른쪽 버튼(B/Circle) |
| Interact  | Button          | E, 상단 버튼(Y/Triangle)           |

### 입력 읽기

반응성을 위해 `Update`에서 입력을 읽는다. 물리 힘은 `FixedUpdate`에서 적용한다.

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerInputHandler : MonoBehaviour
{
    private PlayerInputActions m_controls;
    private Vector2 m_vMoveInput;
    private bool m_bJumpPressed;
    private bool m_bJumpReleased;
    private bool m_bDashPressed;

    private void Awake()
    {
        m_controls = new PlayerInputActions();
    }

    private void OnEnable()
    {
        m_controls.Gameplay.Enable();

        m_controls.Gameplay.Jump.performed += _ctx => m_bJumpPressed = true;
        m_controls.Gameplay.Jump.canceled += _ctx => m_bJumpReleased = true;
        m_controls.Gameplay.Dash.performed += _ctx => m_bDashPressed = true;
    }

    private void OnDisable()
    {
        m_controls.Gameplay.Disable();
    }

    private void Update()
    {
        m_vMoveInput = m_controls.Gameplay.Move.ReadValue<Vector2>();

        // 점프, 대시 등을 처리한다 (코요테 타임, 버퍼링이 여기서 일어남)
        ProcessJump();
        ProcessDash();
    }

    private void LateUpdate()
    {
        // 프레임 끝에서 원샷 플래그를 초기화
        m_bJumpPressed = false;
        m_bJumpReleased = false;
        m_bDashPressed = false;
    }

    private void FixedUpdate()
    {
        // 이동 힘은 여기서 적용
        ApplyMovement(m_vMoveInput);
        ApplyGravity();
    }
}
```

**핵심 규칙:** `performed` 콜백을 절대 `FixedUpdate`에서 읽지 마라. 물리 틱과 렌더 프레임은 일치하지 않으므로 버튼 입력을 놓칠 수 있다. `Update`에서 읽어 플래그에 저장한 뒤 `FixedUpdate`에서 소비하라.

---

## 실전 팁

- **가속/감속 커브**가 즉시 최대 속도로 튀는 것보다 더 좋은 느낌을 준다. 수평 속도에 `Mathf.MoveTowards`나 `Mathf.Lerp`를 사용하라.
- 이동을 조작할 때는 **수평 속도와 수직 속도를 분리**하라. 한 축만 건드리려는 의도인데 전체 속도 벡터를 0으로 만들지 마라.
- **인스펙터에 튜닝 값을 노출**하라 (`[SerializeField]`, `[Header]` 사용). 움직임의 느낌은 계산이 아니라 반복 시행착오로 찾아진다.
- Update에서는 **`Time.deltaTime`**을 사용하라. FixedUpdate에서는 `Time.deltaTime`이 자동으로 `Time.fixedDeltaTime`을 반환하므로 양쪽 맥락 모두에서 동작한다.
- **고스트 플랫폼**(플레이어가 단단한 지면을 뚫고 떨어지는 현상)은 대개 지면 체크 반경이 너무 작거나 캐릭터가 너무 빠르게 움직여서 발생한다. `Physics2D.velocityIterations`를 늘리거나 연속 충돌 감지(Continuous Collision Detection)를 사용하라.
- 움직임 버그 디버깅을 위해 **입력을 기록하고 재생**하라. 입력 프레임을 리스트에 저장했다가 재생하면 문제를 결정적으로(deterministically) 재현할 수 있다.
