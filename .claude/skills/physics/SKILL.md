---
name: physics
description: "Unity PhysX 물리 — 언제 PhysX 대신 자체 Collider 를 쓰는지(1500개 기준), 비할당 쿼리, 레이어 매트릭스, FixedUpdate 규율, 충돌 감지 모드, Rigidbody 설정."
globs: ["**/*Physics*.cs", "**/*Rigidbody*.cs", "**/*Movement*.cs"]
---

# 물리 — PhysX

## 먼저: PhysX 인가, 자체 Collider 인가

| 상황 | 쓰는 것 |
|---|---|
| 주변에서 서로 판정하는 오브젝트가 **1500개 초과**, 또는 PhysX 판정이 프로파일러에서 무거움 | 자체 Collider — [[collider-system]] |
| 그보다 적고 가벼운 판정, 물리 반응(힘·중력·조인트)이 필요한 경우 | PhysX (이 문서) |

이미 자체 Collider 로 판정하는 대상과 엮이는 코드에는 PhysX 를 섞지 않는다.

## FixedUpdate 규율

`Rigidbody`를 움직이는 코드는 `FixedUpdate`에. 연속 입력은 `InputManager`가 `Update`에서 캐싱한 값을 읽는다 (`architecture.md` 입력 시스템).

```csharp
private void FixedUpdate()
{
    Vector2 vMoveDir = InputManager.m_Instance.MoveDir;
    Vector3 vMove = new Vector3(vMoveDir.x, 0.0f, vMoveDir.y);
    m_refRigidbody.MovePosition(m_refRigidbody.position + vMove * m_fSpeed * Time.fixedDeltaTime);
}
```

## 비할당 쿼리

```csharp
private readonly RaycastHit[] m_arrHitBuffer = new RaycastHit[16];
private readonly Collider[] m_arrOverlapBuffer = new Collider[32];

int iHitCount = Physics.RaycastNonAlloc(vOrigin, vDir, m_arrHitBuffer, fMaxDistance, m_tHitLayer);
for (int i = 0; i < iHitCount; ++i)
{
    RaycastHit tHit = m_arrHitBuffer[i];
    // 히트 처리
}

int iOverlapCount = Physics.OverlapSphereNonAlloc(vCenter, fRadius, m_arrOverlapBuffer, m_tHitLayer);
int iCastCount = Physics.SphereCastNonAlloc(vOrigin, fRadius, vDir, m_arrHitBuffer, fMaxDistance, m_tHitLayer);
```

`RaycastAll`, `OverlapSphere`처럼 배열을 새로 만드는 버전은 쓰지 않는다.

## 레이어 충돌 매트릭스

Edit > Project Settings > Physics > Layer Collision Matrix 에서 설정한다 (`ProjectSettings/*.asset` 직접 수정 금지 — MCP `manage_physics`). 코드로 끌 때는 `Physics.IgnoreLayerCollision(iLayerA, iLayerB, true)`.

## 충돌 감지 모드

| 모드 | 사용 시점 |
|---|---|
| Discrete | 느린 오브젝트 (기본값) |
| Continuous | 얇은 콜라이더를 뚫고 지나갈 수 있는 빠른 오브젝트 |
| Continuous Dynamic | 다른 빠른 오브젝트와 충돌하는 빠른 오브젝트 |
| Continuous Speculative | 정확도와 성능의 균형 |

## Collision vs Trigger

- Collision: 양쪽 모두 Collider, 최소 하나가 Rigidbody, 어느 쪽도 트리거가 아님 → `OnCollisionEnter/Stay/Exit(Collision)`
- Trigger: 최소 하나가 `isTrigger` → `OnTriggerEnter/Stay/Exit(Collider)`

## 주의

- `transform.position`을 직접 옮긴 직후에는 다음 물리 스텝 전까지 쿼리가 새 위치를 모른다. 필요하면 `Physics.SyncTransforms()`
- Rigidbody: 플레이어는 `Interpolate`, 그 외 `None`. 빠른 오브젝트는 Continuous
- 2D 는 `Rigidbody2D`, `Physics2D.*NonAlloc`, `OnTriggerEnter2D(Collider2D)`
