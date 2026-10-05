---
name: physics
description: "Unity 물리 — 비할당(non-allocating) 쿼리, 충돌 레이어, FixedUpdate 규율, 연속 충돌 감지, 캐릭터 컨트롤러, 조인트."
globs: ["**/*Physics*.cs", "**/*Collider*.cs", "**/*Rigidbody*.cs", "**/*Trigger*.cs"]
---

# 물리 시스템

## FixedUpdate 규율

모든 물리 코드는 `FixedUpdate`에 작성한다. 입력을 읽는 작업은 `InputView.Update`에서 이루어지고(아키텍처 규칙 참고), 그 값이 System으로 전달되면 System은 캐싱해둔 값을 사용해 `FixedUpdate`에서 힘을 적용한다.

```csharp
// InputView.cs — Update에서 입력을 읽어 System으로 전달
private void Update()
{
    Vector2 vMoveInput = m_controls.Player.Move.ReadValue<Vector2>();
    m_refPlayerSystem.SetMoveInput(vMoveInput);
}

// PlayerView.cs — System이 캐싱해둔 입력값으로 FixedUpdate에서 물리를 적용
private Vector2 m_vMoveInput;

public void SetMoveInput(Vector2 _vInput) => m_vMoveInput = _vInput;

private void FixedUpdate()
{
    m_refRigidbody.AddForce(m_vMoveInput * m_fForce);
}
```

## 비할당(Non-Allocating) 쿼리

```csharp
// 버퍼는 미리 할당해둔다
private static readonly RaycastHit[] m_arrHitBuffer = new RaycastHit[16];
private static readonly Collider[] m_arrOverlapBuffer = new Collider[32];

// Raycast
int iHitCount = Physics.RaycastNonAlloc(vOrigin, vDirection, m_arrHitBuffer, fMaxDistance, layerMask);
for (int i = 0; i < iHitCount; i++)
{
    RaycastHit hit = m_arrHitBuffer[i];
    // 히트 결과 처리
}

// Overlap sphere (영역 감지)
int iOverlapCount = Physics.OverlapSphereNonAlloc(vCenter, fRadius, m_arrOverlapBuffer, layerMask);

// Sphere cast (두께가 있는 raycast)
int iCastCount = Physics.SphereCastNonAlloc(vOrigin, fRadius, vDirection, m_arrHitBuffer, fMaxDistance, layerMask);
```

## 레이어 충돌 매트릭스

```csharp
// 코드로 레이어 간 충돌을 무시하기
Physics.IgnoreLayerCollision(iPlayerLayer, iPickupLayer, true);

// 또는 Edit > Project Settings > Physics > Layer Collision Matrix에서 직접 설정
```

레이어 구성 예시:
```
6: Player
7: Ground
8: Enemy
9: Projectile
10: Trigger (물리 충돌 없음, 트리거만 발생)
11: Interactable
```

## 충돌 감지 모드

| 모드 | 사용 시점 |
|------|-----------|
| Discrete | 느린 오브젝트(기본값) |
| Continuous | 얇은 콜라이더를 뚫고 지나갈 수 있는 빠른 오브젝트 |
| Continuous Dynamic | 다른 빠른 오브젝트와 충돌하는 빠른 오브젝트 |
| Continuous Speculative | 정확도와 성능의 균형이 좋은 방식 |

## Collision vs Trigger 콜백

```csharp
// Collision (양쪽 모두 콜라이더를 갖고, 최소 하나가 Rigidbody를 가지며, 어느 쪽도 트리거가 아닌 경우)
private void OnCollisionEnter(Collision collision) { }
private void OnCollisionStay(Collision collision) { }
private void OnCollisionExit(Collision collision) { }

// Trigger (최소 하나의 콜라이더가 isTrigger = true인 경우)
private void OnTriggerEnter(Collider other) { }
private void OnTriggerStay(Collider other) { }
private void OnTriggerExit(Collider other) { }
```

## Physics.SyncTransforms

트랜스폼을 직접 이동시킨 직후에는 다음 물리 스텝 전까지 물리 쿼리가 새 위치를 반영하지 못한다. 강제로 동기화하려면:
```csharp
transform.position = vNewPosition;
Physics.SyncTransforms(); // 이제 raycast가 새 위치를 인식한다
```

## Rigidbody 설정

- **Interpolation:** 플레이어는 `Interpolate`(물리 스텝 사이를 부드럽게 보간), 그 외에는 `None`
- **Constraints:** 3D에서 2D와 유사하게 동작시키려면 회전을 고정(Freeze)
- **Collision Detection:** 빠르게 움직이는 오브젝트는 Continuous로 설정

## 2D 물리 대응표

| 3D | 2D |
|----|-----|
| `Rigidbody` | `Rigidbody2D` |
| `BoxCollider` | `BoxCollider2D` |
| `Physics.Raycast` | `Physics2D.Raycast` |
| `Physics.OverlapSphereNonAlloc` | `Physics2D.OverlapCircleNonAlloc` |
| `OnCollisionEnter(Collision)` | `OnCollisionEnter2D(Collision2D)` |
| `OnTriggerEnter(Collider)` | `OnTriggerEnter2D(Collider2D)` |

## 조인트(Joints)

| 조인트 | 용도 |
|-------|-----|
| Fixed | 오브젝트끼리 용접하듯 고정 |
| Hinge | 문, 바퀴 |
| Spring | 탄성 있는 연결 |
| Configurable | 모든 축을 완전히 제어 |
| Character | 물리 기반 캐릭터 컨트롤러 |
