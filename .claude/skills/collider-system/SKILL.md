---
name: collider-system
description: "자체 충돌 판정 시스템(ColliderManager + CircleCollider/ObbCollider + BoxColliderGrid, Burst Job) 사용·설치 가이드. 주변에서 서로 판정하는 오브젝트가 1500개를 넘거나 PhysX 판정이 무거울 때 PhysX 대신 이걸 쓴다. 탄·몬스터 피격, 범위 탐색(FindAllInRadius/FindNearest), 레이 판정(RaycastMask) 코드를 쓸 때 사용합니다."
globs: ["**/*Collider*.cs", "**/*Bullet*.cs", "**/*Laser*.cs", "**/*Scanner*.cs", "**/*Missile*.cs"]
---

# 자체 Collider — ColliderManager

PhysX 없이 원(구)·회전 박스 판정을 Burst Job 으로 돌리는 시스템.

## 언제 쓰나

- **주변에서 서로 판정하는 오브젝트가 1500개를 넘거나**, PhysX 트리거/쿼리가 프로파일러에서 무거우면 이것을 쓴다
- 그보다 적고 가벼우면 PhysX `*NonAlloc` 쿼리로 충분하다 ([[physics]])
- 이미 이 시스템으로 판정하는 대상과 엮이는 코드에는 PhysX 를 섞지 않는다

## 구성

| 파일 | 역할 |
|---|---|
| `BaseCollider` | 공통 베이스 — ID/레이어 캐싱, `OnEnable`/`OnDisable`에서 자동 Activate/UnActivate, `OnHitTargetEnter`/`Stay`/`Exit` (R3) |
| `CircleCollider` | 원(구) — `m_fRadius`, `m_vOffset`(피벗 보정) |
| `ObbCollider` | 회전 박스 — `m_vBaseHalfExtent`, `m_vOffset` (주로 정적 장애물) |
| `ColliderManager` | 싱글톤. 레이어 충돌 매트릭스, SoA + `GridOverlapJob`, Enter/Stay/Exit 판정, 쿼리 API. `[DefaultExecutionOrder(1000)]` — `LateUpdate`에서 Complete |
| `ColliderManagerScheduler` | `ScheduleFrame()`을 프레임 맨 앞(-1000)에서 호출. `ColliderManager.Awake`가 자동으로 붙인다 |
| `ColliderCenterRefresher` | `TransformAccessArray` + Job 으로 중심/축 갱신 |
| `BoxColliderGrid` | 활성 콜라이더 전체를 담는 단일 정적 공간분할 그리드 (카운팅 소트) |
| `Editor/ColliderManagerEditor` | 레이어 매트릭스 인스펙터 |

소스 위치 — 이 저장소: `Assets/3D/02_Player/Weapon/`(Base/Circle/Obb), `Assets/3D/05_Manager/`(나머지). 다른 프로젝트: 이 스킬 폴더의 `reference/`.
필요 패키지: Burst, Collections, R3.

## 다른 프로젝트에 설치

1. `reference/`의 `.cs`를 `Assets/` 아래로 복사 (`Editor/`는 Editor 폴더 그대로)
2. `ColliderManager.Start()`의 `m_refPlayer = Player.CurrentPlayer.transform;` 한 줄을 그 프로젝트의 플레이어 Transform 으로 바꾸거나 지운다 — Box 컬링 기준점이고, 비워두면 컬링 없이 전부 판정한다
3. 씬 루트에 `ColliderManager` 오브젝트를 두고 인스펙터 레이어 매트릭스 설정 — 레이어 i 가 충돌할 레이어 마스크, 한쪽만 체크해도 양방향
4. 판정할 오브젝트에 `CircleCollider`/`ObbCollider`를 붙이고 PhysX `Collider`/`Rigidbody`는 뗀다

## 피격 이벤트

```csharp
[SerializeField] private CircleCollider m_refCircleCollider;
private DisposableBag m_bagEvents;

private void OnEnable()
{
    m_refCircleCollider.OnHitTargetEnter.Subscribe(Attack).AddTo(ref m_bagEvents);
}

private void OnDisable()
{
    m_bagEvents.Clear();
}

private void Attack(BaseCollider _refOther)
{
    if (_refOther.TryGetComponent(out IDamageable refDamageable) == false)
        return;
    // ...
}
```

- payload 는 `UnityEngine.Collider`가 아니라 상대 `BaseCollider`
- 콜백은 `ColliderManager.LateUpdate`(Job Complete 후)에서 온다
- 레이어는 `Awake`에서 캐싱한다 — 런타임에 `gameObject.layer`를 바꿔도 반영되지 않는다
- 풀링 오브젝트는 켜지고 꺼질 때 자동으로 등록/해제된다 — 직접 `Activate`를 부르지 않는다

## 쿼리 (Circle 전용, 무할당)

결과 리스트는 필드로 미리 만들어 재사용한다. 호출 시 리스트를 먼저 `Clear()`한다.

```csharp
private readonly List<CircleCollider> m_listHitBuffer = new List<CircleCollider>(16);

ColliderManager.m_Instance.FindNearest(transform.position, m_fFindRadius, m_tFindLayer, out CircleCollider refNearest);
ColliderManager.m_Instance.FindAllInRadius(vPos, fRadius, m_tHitLayer, m_listHitBuffer);
ColliderManager.m_Instance.RaycastMask(vOrigin, vDir, fLength, fBeamRadius, m_tHitLayer, m_listHitBuffer);   // 원점 거리순 (관통 순서)
bool bHit = ColliderManager.m_Instance.RaycastMask(vOrigin, vDir, fLength, m_tHitLayer, out CircleCollider refHit);
```

| PhysX | 이 시스템 |
|---|---|
| `Physics.Raycast` | `RaycastMask(..., out CircleCollider)` |
| `Physics.RaycastNonAlloc` | `RaycastMask(..., fBeamRadius, mask, List)` |
| `Physics.OverlapSphereNonAlloc` | `FindAllInRadius` |
| 최근접 탐색 | `FindNearest` |

## 주의

- 한 오브젝트에 PhysX `Collider`/`Rigidbody`를 같이 남기지 않는다 — `Physics.SyncColliderTransform` 비용이 그대로 남는다
- 서브클래스에서 `Start()`를 쓰면 반드시 `protected override void Start()` + `base.Start()` — `private void Start()`로 가리면 등록이 영원히 안 된다 (실제 버그)
- 레이어 하나에는 한 가지 도형만 넣는다
