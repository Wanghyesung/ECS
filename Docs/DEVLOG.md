# 🚀 [유니티 우주 슈팅] 몬스터 AI 비헤이비어 트리 & 지속 효과 구현

- **날짜:** 2026-06-27
- **관련 시스템:** AI, Behavior Tree, Blackboard, Optimization

## 1. 🚨 직면했던 아쉬운 점 / 문제점

- 기존 `Monster.cs` 구조에서 상태 이상(eStatusEffect)이나 패시브 버프처럼 '일정 시간 지속되는 효과'를 처리하는 루프가 모호했음.
- 이를 `Update()`에 무지성으로 넣으면 기껏 구축한 BT 아키텍처 규칙이 깨짐

## 2. 💡 해결 아이디어 (설계 제어)

- 메인 행동 트리와 별개로 독립적인 패시브 루프를 분리하거나, BT 최상단에 **병렬(Parallel) 복합 노드**를 도입하여 결합도를 낮추는 방안 도출.
- 데이터 오염을 막기 위해 런타임 데이터는 철저히 `Blackboard`에서 관리하도록 제약 조건 유지.

## 3. 🛠️ 반영된 핵심 코드 변경 사항

### 🔹 GuidedBullet.cs (최적화)

- `m_refTarget`을 매 프레임 `transform.position`으로 접근하던 방식을 `m_refTargetTr` 트랜스폼 캐싱 방식으로 수정

### 🔹 Monster.cs (구조 개선)

- 비트 연산 기반의 상태 이상 처리 로직 가다듬음.
- (클로드가 요약해 준 패시브/BT 관련 수정 내역을 여기에 추가)

---

# 🚀 [Unity] Rigidbody 이동/회전 시 멈춤 및 진동 현상 해결 (Transform 충돌)

## 📌 1. 문제 상황 요약

- **증상**: 유도탄(GuidedBullet)이 이동할 때 타겟을 바라보도록 회전시키면, 속도가 급격히 줄어들거나 제자리에 멈추는(또는 심하게 진동하는) 현상 발생.
- **원인**: `Update()`에서 `Transform`을 직접 수정하는 회전 연산과, `FixedUpdate()`에서 `Rigidbody`를 사용하는 이동 연산이 서로 충돌하여 물리 엔진이 회전값을 매 프레임 초기화(롤백)함.

## 🔍 2. 핵심 원인 분석 (유니티의 두 개의 세상)

유니티에서 오브젝트는 좌표를 담당하는 `Transform`과 물리 엔진(PhysX)이 관리하는 `Rigidbody` 내부 상태(Internal State)를 가집니다. 이 두 시스템의 박자가 맞지 않아 발생한 문제입니다.

## 🔄 프레임별 오류 발생 순서

1. **`Update()` 단계 (`transform.LookAt()`)**
    - 물리 엔진을 우회하여 `Transform.rotation`을 강제로 타겟 방향으로 변경합니다.
    - ⚠️ **문제**: `Rigidbody` 내부의 물리 회전 데이터는 이 변화를 감지하지 못하고 옛날 방향을 그대로 유지합니다.

2. **`FixedUpdate()` 단계 (`Rigidbody.MovePosition()`)**
    - 코드 내에서 전방 방향 벡터인 `transform.forward`를 참조해 이동 거리를 계산합니다.
    - `LookAt`이 방금 적용되었으므로, 일단 타겟 방향으로 아주 잠깐 이동을 요청합니다.

3. **물리 프레임 마무리 단계 (동기화 - ★가장 중요★)**
    - `FixedUpdate`가 끝나는 시점에 유니티 물리 엔진은 **`Rigidbody`가 기억하고 있던 옛날 회전값으로 `Transform`을 덮어써 버립니다(롤백).**

4. **무한 루프 (총알 멈춤 현상)**
    - 방향 수정 ➡️ 물리 엔진이 옛날 방향으로 강제 롤백 ➡️ 다음 프레임에서 또 롤백된 방향 기준(`transform.forward`)으로 이동...
    - 이 과정이 매 프레임 찌그러지듯 반복되면서 물리적 연산 오차가 발생하고, 플레이어 눈에는 **총알이 제자리에 멈추거나 진동하는 것**처럼 보이게 됩니다.

## 🛠️ 3. 해결 방법: 시스템의 단일화

`Update`의 `transform.LookAt()`을 제거하고, 모든 물리(이동/회전) 처리를 **`FixedUpdate()` 내부에서 Rigidbody 전용 메서드로 통일**합니다.

---

# 행동 트리 SO 기반 노드 인덱스 잔존 및 에디터 메모리 오염 버그

## ❌ 문제 분석 (Bug Analysis)

### 1. [버그 1] `SOSelectNode` 실행 실패(Failure) 시 인덱스 리셋 누락

- **현상:** Selector 노드가 자식 노드들을 순회하다가 최종적으로 `Failure`를 반환할 때, 현재 실행 중이던 인덱스(`iCurrentIdx`)를 `0`으로 초기화하지 않아 다음 실행 시 오작동 발생.
- **Sequence vs Selector 비교:**

    | **반환 상태** | **SOSequenceNode** | **SOSelectNode** |
    | --- | --- | --- |
    | **Success** | `iCurrentIdx = 0` ✅ | `iCurrentIdx = 0` ✅ |
    | **Running** | `iCurrentIdx = i` ✅ | `iCurrentIdx = i` ✅ |
    | **Failure** | `iCurrentIdx = 0` ✅ | **리셋 없음 (버그 발생) ❌** |

- **버그 재현 시나리오 (Timeline):**

    ```
    프레임 1: Selector → Node[0] Fail, Node[1] Running → iCurrentIdx = 1
    프레임 2: Selector → Node[1] Fail (조건 불충족) → 루프 종료 → Failure 반환
    → 이 때 iCurrentIdx가 1로 남음
    프레임 3: Selector → iCurrentIdx = 1이므로 Node[1]부터 실행 ← 잘못된 노드가 먼저 실행
    ```

### 2. [버그 2] Unity 에디터 재생(Play) 간 SO 에셋 상태 유지

- **현상:** 버그 1을 방지하고자 코드 레벨에서 초기화 필드나 `Awake()`를 활용했으나, 에디터를 재시작(Play 버튼 클릭)해도 `iCurrentIdx`가 여전히 이전 세션의 값으로 남아있음.
- **원인:** Unity 에디터에서 `ScriptableObject`는 **에셋(Asset)** 형태로 메모리에 계속 로드되어 있습니다. 플레이 모드를 껐다 켜도 C# 객체가 새로 생성되는 것이 아니므로, **필드 초기화식이나 `Awake()`는 호출되지 않고 이전 세션의 메모리 상태가 그대로 유지**됩니다.

## 🛠️ 해결책 (Solution)

### 1. `SOSelectNode` 루프 탈출 시 초기화 코드 추가

루프가 끝나고 최종적으로 `Failure`를 반환하기 직전, `iCurrentIdx`를 확실하게 `0`으로 리셋해 줍니다.

```csharp
// SOSelectNode.cs - 수정 후
public override eNodeState Execute(BlackBoard _refBB)
{
    for (int i = iCurrentIdx; i < listNode.Count; ++i)
    {
        eNodeState eState = listNode[i].Execute(_refBB);

        if (eState == eNodeState.Success) { iCurrentIdx = 0; return Success; }
        else if (eState == eNodeState.Running) { iCurrentIdx = i; return Running; }
        // Failure → 그냥 다음 루프로 넘어감
    }

    // [Fix] 최종 실패 시에도 다음 실행을 위해 인덱스를 0으로 초기화
    iCurrentIdx = 0;
    return eNodeState.Failure;
}
```

### 2. `Awake()` 대신 `OnEnable()`을 통한 에디터 세션 초기화

`ScriptableObject`는 에디터가 플레이 모드로 진입할 때 도메인 재로드(Domain Reload)와 함께 `OnEnable()`이 다시 호출되는 특성이 있습니다. 이를 이용해 세션이 바뀔 때마다 값을 강제 초기화합니다.

```csharp
// SOSequenceNode.cs, SOSelectNode.cs 공통 수정
private void OnEnable()
{
    // [Fix] Unity 에디터 플레이 진입 시 메모리 오염 방지를 위한 초기화
    iCurrentIdx = 0;
}
```

## 💡 인사이트 및 요약 (Key Takeaways)

> 💡 **핵심 요약**
>
> 1. **흐름 제어 노드(Sequence, Selector)의 탈출 조건:** 제어 노드가 끝까지 돌아서 최종 상태(`Success` 혹은 `Failure`)를 반환할 때는 반드시 내부 진행 인덱스 변수를 상태 관리 스코프에 맞춰 초기화해야 한다.
> 2. **ScriptableObject의 생명주기:** SO는 에디터 상에서 '데이터 에셋'으로 작동하므로 일반 `MonoBehaviour`처럼 `Awake()`나 생성자 시점 초기화에 의존하면 안 된다. **런타임 세션 초기화가 필요하다면 `OnEnable()`을 적극 활용하자.**

---

# 🚀 [Unity 우주 슈팅] 호밍 미사일 공전 버그 / VisualObject 재사용성 개선 / 몬스터 HP UI 구조 설계

- **날짜:** 2026-07-09
- **관련 시스템:** Weapon/Missile, Visual/Presentation, UI, Combat

## 1. 🚨 문제 상황

1. 미사일이 타겟을 락온하면 명중하지 않고 주변을 계속 도는(공전) 현상 발생
2. `VisualObject`(롤 연출 컴포넌트)가 `InputManager`를 직접 참조하고 있어 플레이어 전용으로 고정되어, 몬스터 등 다른 오브젝트에 재사용 불가능
3. 몬스터 HP를 UI(HPSlider)에 보여줄 때, 그 책임을 `BattleManager`/`UIManager` 중 누가 가져야 할지 구조 미정

## 2. 🔍 원인 분석

### (1) 미사일 공전

`Missiles.cs`는 순수 추적(Pure Pursuit) 방식으로 매 프레임 타겟 방향으로 회전한다. 회전속도로 정해지는 선회 반경(속도 / 각속도)이 도착 판정 임계값(`1.0f` 고정)보다 크면, 근접 시 타겟을 정면으로 겨냥하지 못해 궤도에 갇혀버린다. 게다가 거리가 가까워질수록 회전을 더 세게 주려던 `fDistAccel` 계산식이 주석 처리되어 있어 근접 시 회전력도 부족했다.

```csharp
// 수정 전 (fDistAccel이 거리와 무관하게 고정 배율)
float fDistAccel = /*(fTargetLength / fDist) * */fBaseSpeed * 0.5f;
...
if (fDist <= Mathf.Max(fMoveDist, 1.0f)) { ... } // 도착 판정이 선회 반경보다 훨씬 작음
```

### (2) VisualObject 하드 커플링

```csharp
// 수정 전 VisualObject.LateUpdate()
float fX = InputManager.m_Instance.InputInfo.MoveDir.x;
```
연출 모듈이 입력 시스템을 직접 알아야 하는 구조라, 몬스터 이동 방향에 따른 롤 연출을 붙이려면 `VisualObject` 자체를 뜯어고쳐야 했음.

### (3) 몬스터 HP UI 소유권

전투 로직(피격 판정 → 데미지 적용)은 이미 `Bullet`/`TriggerObject` → `IDamageable.TakeDamage`로 완결되어 있어서, `BattleManager`를 새로 만들면 아무 책임 없는 중계자만 추가되는 셈. 진짜 문제는 "UI 갱신 권한을 누가 가지냐"였음.

## 3. 🛠️ 해결 및 코드 변경

### 🔹 Missiles.cs / SOAttackInfo.cs — 공전 버그 수정

- `SOAttackInfo` / `AttackInfo`에 `ProximityRadius`(기본 1.5) 필드 추가 — 근접신관 개념의 명중 판정 반경
- `Missiles.cs`: 발사 시점 초기 거리(`m_fTargetLength`)를 저장해 `(m_fTargetLength / fDist)` 비율로 거리 기반 회전 가속을 복구, 도착 판정을 `ProximityRadius` 기준으로 변경

```csharp
float fArriveDist = Mathf.Max(fMoveDist, m_refAttackInfo.ProximityRadius);
if (fDist <= fArriveDist) { ... }

float fDistAccel = (m_fTargetLength / fDist) * fBaseSpeed * 0.5f; // 근접할수록 회전 가속
```

### 🔹 VisualObject.cs / PlayerMovement.cs / Monster.cs — 인터페이스 기반 디커플링

`IRollable` 인터페이스(`RollDirX` 프로퍼티)를 도입해 연출 로직과 입력 소스를 분리.

```csharp
public interface IRollable
{
    float RollDirX { get; }
}
```

- `PlayerMovement`가 구현: `RollDirX => m_vInput.x`
- `Monster`도 구현: `RollDirX => 0.0f` (추후 이동 방향 연동 가능)
- `VisualObject`는 `m_refOwner` 필드를 없애고 `Awake()`에서 `GetComponentInParent<IRollable>()`로 자동 탐색, `LateUpdate()`는 그 값만 읽음

결과적으로 `VisualObject`는 InputManager나 Player를 전혀 모르는 순수 연출 모듈이 되어, 부모 오브젝트가 `IRollable`만 구현하면 어떤 엔티티에도 재사용 가능해짐.

### 🔹 MonsterHPBar.cs (신규) / Monster.cs — 몬스터 HP UI 구조

- **BattleManager는 도입하지 않음**: 전투 로직은 이미 완결되어 있어 불필요한 중계 레이어가 됨.
- **몬스터 HP UI는 "현재 공격 중인 몬스터 1마리"만 보여주는 기획** → 구독자가 하나뿐이라 옵저버/이벤트버스보다, 기존 `Player.cs`가 쓰던 `CameraManager.m_Instance.StartShakeCamera(...)`와 동일한 **직접 싱글턴 호출** 패턴을 채택.
- **플레이어 HP는 이벤트 기반으로 남겨두기로 의도적으로 비대칭 설계**: 게임오버 체크, 저체력 경고 등 향후 구독자가 늘어날 여지가 몬스터보다 크기 때문.

```csharp
// Monster.cs — TakeDamage
public void TakeDamage(AttackInfo _refAttackInfo)
{
    if (m_refBlackBoard.ObjInfo.State == eEntityState.Hit)
        return;

    m_refBlackBoard.ObjInfo.CurrentHP -= _refAttackInfo.Damage; // 기존엔 누락되어 있던 HP 차감
    MonsterHPBar.m_Instance?.ShowHp(this, m_refBlackBoard.ObjInfo.CurrentHP, m_SOMonsterInfo.MaxHP);
    ...
}
```

`MonsterHPBar`는 `CameraManager`와 동일한 싱글턴 패턴으로, 타겟이 바뀌면 슬라이더를 리셋하고 같은 타겟이면 값만 갱신하며, 마지막 피격 후 `m_fHideDelay`(기본 2초) 경과 시 자동으로 숨긴다.

**남은 작업**: 씬에 `MonsterHPBar` 오브젝트를 배치하고 `m_refRoot`(표시/숨김 컨테이너), `m_refSlider`(`SliderImage`)를 인스펙터에서 연결해야 실제로 동작함.

## 4. 💡 인사이트 및 요약

> 💡 **핵심 요약**
>
> 1. **호밍 유도 로직에서 선회 반경 vs 판정 반경**: 순수 추적(Pure Pursuit) 방식은 선회 반경이 판정 반경보다 크면 타겟 주변을 도는 안정 궤도에 빠진다. 근접 시 회전 가속을 강하게 주거나 판정 반경을 선회 반경 수준으로 키워야 한다.
> 2. **연출(Visual) 모듈은 데이터 소스를 몰라야 재사용 가능**: `VisualObject`가 `InputManager`를 직접 참조하던 것을, 인터페이스(`IRollable`)를 통해 "부모가 값을 제공한다"는 계약으로 바꾸자 플레이어 전용 컴포넌트가 범용 컴포넌트가 됨.
> 3. **옵저버 패턴이 항상 정답은 아니다**: 구독자가 정확히 하나뿐이고 앞으로도 그럴 대상(현재 타겟 몬스터 HP UI)은, 이벤트 버스보다 기존 컨벤션(`CameraManager` 직접 호출)을 따르는 단순한 싱글턴 호출이 더 적합하다. 반대로 구독자가 늘어날 여지가 있는 대상(플레이어 HP)은 이벤트 기반으로 남겨두는 비대칭 설계가 합리적일 수 있다.

---

# 🚀 [Unity 우주 슈팅] AttackInfo 공유 참조 버그 → ShotInfo(struct) 분리

- **날짜:** 2026-07-14
- **관련 시스템:** Weapon/Bullet, Combat, Data Architecture

## 1. 🚨 문제 상황

- 레이저처럼 발사 후에도 플레이어를 계속 따라가야 하는 무기를 만들려다, "Weapon 쪽에 bool 플래그를 늘려서 자식으로 붙일지 말지 분기하면 코드가 더러워지지 않을까?"라는 질문에서 출발.
- 논의 중 더 근본적인 문제를 발견: `Weapon.Awake()`가 `AttackInfo`를 **딱 한 번만** 생성해 `m_refAttackInfo` 필드에 저장해두고, 발사할 때마다 `SetAttack()`에 **그 동일한 참조**를 그대로 넘기고 있었음.
- `AttackInfo`가 `class`(참조 타입)라서, 같은 무기에서 나간 총알들은 전부 메모리상 같은 인스턴스를 공유. `MoveDir`, `HitPosition`, `TargetPos`, `TargetTrasnform`처럼 총알 개별 생애주기에 종속돼야 할 값들이 전부 이 공유 객체에 뒤섞여 있었음.
- 지금까지 티가 안 났던 이유:
  - `HitPosition`은 쓰기만 하고 아무도 읽지 않는 죽은 필드였음.
  - `Monster`/`Player`의 `CoNockback` 코루틴은 첫 `yield return` 이전에 필요한 값을 전부 로컬 변수로 스냅샷 떠서 우연히 안전했음.
- 하지만 관통 총알의 `HitCount`처럼 **총알 하나의 생애 동안 누적되는 값**을 그대로 추가하면, 동시에 날아가는 총알 A/B가 같은 카운터를 공유해 서로의 히트 수를 간섭하는 버그가 바로 재현됨.

## 2. 💡 해결 아이디어

- `AttackInfo`를 통째로 `struct`로 바꾸는 방법도 검토했지만, "무기당 한 번 세팅되고 안 바뀌는 공유 설정"과 "총알 한 발마다 새로 생기는 동적 데이터"의 책임이 여전히 한 타입에 섞여있는 문제는 남음.
- 대신 두 개념을 완전히 분리:
  - **`AttackInfo` (class, 그대로 유지)**: `Damage`, `AttackSpeed`, `CoolDown`, `KnockbackForce/Duration`, 호밍 관련 필드, `HitLayers`, `Owner` 등 Weapon당 한 번 세팅되고 이후 절대 안 바뀌는 값. 참조 공유해도 안전.
  - **`ShotInfo` (struct, 신설)**: `TargetPos`, `TargetTr`, `MoveDir`, `HitPosition`, `HitCount` 등 발사/피격마다 새로 생기는 값.
- `struct`를 선택한 이유는 `new` 힙 할당 없이(GC-Alloc-0 유지) `SetAttack()` 호출 시 C#이 자동으로 값 복사를 해주기 때문 — 총알마다 독립된 복사본을 갖게 되어 공유 버그가 원천적으로 사라짐.

## 3. 🛠️ 반영된 핵심 코드 변경 사항

### 🔹 SOAttackInfo.cs

- `AttackInfo`에서 `TargetPos`/`TargetTrasnform`/`HitPosition`/`MoveDir` 제거.
- 신규 `ShotInfo` struct 추가:

```csharp
[Serializable]
public struct ShotInfo
{
    public Vector3 TargetPos;
    public Transform TargetTr;
    public Vector3 MoveDir;
    public Vector3 HitPosition;
    public int HitCount;
}
```

### 🔹 Bullet.cs / Laser.cs / Missiles.cs / GuidedBullet.cs

- `IAttackObject.SetAttack(AttackInfo, ShotInfo)`로 시그니처 변경, 각 구현체가 `m_refShotInfo` 필드를 자체적으로 보유.
- `Missiles`/`GuidedBullet`의 호밍 로직에서 읽던 `TargetTrasnform`/`TargetPos`/`MoveDir`을 전부 `m_refShotInfo` 쪽으로 이동.

### 🔹 Weapon.cs

- `Fire()`/`FireCircularSector()`/`FireAndRotate()`가 발사마다 로컬 `ShotInfo`를 새로 만들어 `SetAttack(m_refAttackInfo, refShotInfo)`로 전달. `m_refAttackInfo`(공유 설정)는 그대로 재사용.

### 🔹 Monster.cs / Player.cs

- `IDamageable.TakeDamage(AttackInfo, ShotInfo)`로 확장.
- `CoNockback` 코루틴이 `MoveDir`을 `_refAttackInfo`가 아닌 `_refShotInfo`에서 읽도록 수정.

## 4. 💡 인사이트 및 요약

> 💡 **핵심 요약**
>
> 1. **공유 참조 버그는 "쓰기만 하고 아무도 안 읽는 필드"에 숨어있을 수 있다**: `HitPosition`은 당장 아무 문제도 안 일으켰지만, 나중에 누군가 그 값을 읽는 코드를 추가하는 순간 바로 터지는 시한폭탄이었음. 필드가 안전한지는 "지금 안 터진다"가 아니라 "구조적으로 공유되지 않는다"로 판단해야 함.
> 2. **"무기당 한 번 세팅되는 공유 설정" vs "발사마다 새로 생기는 동적 데이터"는 타입 레벨에서 분리하는 게 맞다**: SO는 데이터/에디터 세팅만, 런타임 동적 상태는 Blackboard나 인스턴스에 두라는 프로젝트 원칙(CLAUDE.md)을 Weapon/Bullet 쪽에도 그대로 적용한 사례.
> 3. **struct는 "매번 복사해도 되는 작은 런타임 데이터"에 GC-Alloc-0을 유지하면서 값 격리를 얻는 좋은 도구**: `new` 없이 함수 호출 시 자동으로 독립된 복사본이 생기므로, 공유하면 안 되는 값을 클래스에 넣고 매번 clone하는 것보다 struct로 빼는 편이 더 저렴하고 명확하다.

---

# 🚀 [Unity 우주 슈팅] 명중 시 능력치 발동 시스템 — Weapon vs Bullet, Pool 재사용, Laser 상속 문제

- **날짜:** 2026-07-16
- **관련 시스템:** Weapon/Bullet, Object Pool, Feature(레벨업), Combat

## 1. 🚨 문제 상황

레벨업 능력치로 "총알이 관통한다", "적중 시 총알이 다방면으로 생성된다" 같은 걸 넣으려는데, 기존 구조로는 세 가지 지점에서 막혔다.

1. 총알 도착(명중/AliveTime 만료) 시 실행되는 `SOBulletArriveAction[]`이 **Bullet 프리팹 인스펙터에 직접 박혀있는 구조**라, 레벨업으로 동적으로 붙었다 빠지는 능력치를 표현할 방법이 없었음.
2. 관통 기능을 검토하다 보니 `Bullet.AttackMonster()`가 `AttackInfo.MaxHitCount`를 사실상 무시하고 명중하자마자 무조건 `ObjectPool.PushObject`를 호출하는 **기존 버그**를 발견 (관통을 염두에 둔 필드는 있는데 실제로 안 먹힘).
3. "적중 시 다방면 발사" 카드(`SO_FeatureHitCreateBullet`, `eFeatureID.HitCreateBullet`)가 실제로는 스크립트 연결이 잘못돼 있어서 선택해도 HP 회복만 되는 상태였음(레벨업 카드 시스템 전수 조사 중 발견).

## 2. 💡 설계 논의 (대화로 좁혀나간 과정)

**1) Weapon vs Bullet, 능력치 배열을 어디에 둘 것인가**
- 처음 아이디어는 "원본 프리팹에 Action을 동적으로 꽂아주자"였는데, 총알 종류가 계속 늘어나는(현재 기준 프리팹 수십~수백 개 규모) 상황에서 프리팹 하나하나에 능력치를 세팅하는 건 확장성이 없다고 판단.
- 결론: **소스 오브 트루스를 Weapon(개수가 훨씬 적음)에 두고, 발사 시점에만 총알 인스턴스에 얹어준다.**

**2) Object Pool 재사용 함정**
- Weapon이 발사할 때마다 능력치를 "추가(Add)"하는 방식으로 갔다면, 같은 풀 인스턴스가 재사용될수록 이전에 추가한 게 안 지워지고 계속 쌓여서 **같은 능력치가 중복 실행**되는 버그가 났을 것.
- 해결: 프리팹 고유 동작(baseline, `[SerializeField]`)과 Weapon이 부여하는 동적 동작(런타임 전용)을 **배열 자체를 분리**해서 관리하고, Weapon은 발사할 때마다 자기 배열 참조를 **통째로 덮어쓰기**만 함. Add가 아니라 대입이라 몇 번을 재사용해도 중복이 없고, `new` 없이 참조만 옮기니 GC Alloc도 0.

**3) Arrive/Hit을 클래스 계층으로 나눌 필요가 있는가**
- 처음엔 "도착 시" 로직(`SOBulletArriveAction`)과 별개로 "명중 시" 로직을 위한 새 클래스 계층(`SOBulletHitAction`)을 만들려고 했는데, 두 Action 모두 `Execute(owner)` 시그니처가 완전히 동일하다는 걸 재확인.
- 결론: 공통 베이스 SO 하나(`SOBulletAction`)만 두고, "언제 실행되는가"는 그냥 **배열 소속(Arrive용 배열 vs Hit용 배열)**으로만 구분. 클래스 계층을 늘리지 않아 SO 에셋도 그대로 재사용 가능.

**4) Laser는 Bullet을 상속하지 않는다**
- Weapon이 능력치를 주입할 때 `GetComponent<Bullet>()`으로 구현했는데, `Laser.cs`(`Assets/03_Monster/Bullet/Laser.cs`)가 `Bullet`을 상속하지 않고 `IAttackObject`를 독립적으로 구현하고 있다는 걸 뒤늦게 확인 — Laser 타입 무기는 능력치가 조용히 안 먹히는 상태였음.
- Arrive(도착)는 Laser에는 아예 없는 개념(빔이라 "도착"이 없음)이라 Bullet 전용으로 남기고, Hit(명중)은 Bullet/Laser 둘 다 실제로 존재하는 공통 이벤트라 `IAttackObject` 인터페이스 계약으로 끌어올림. 단, 인터페이스는 필드를 가질 수 없어서 실제 저장 필드는 Bullet과 Laser가 각자 보유하고, 인터페이스엔 `SetWeaponHitActions(...)` 메서드 계약만 선언.

## 3. 🛠️ 반영된 핵심 코드 변경 사항

### 🔹 IAttackObject (Bullet.cs)
```csharp
public interface IAttackObject
{
    public void SetAttack(AttackInfo _refAttackInfo, tShotInfo _refShotInfo);
    public AttackInfo AttackInfo { get; }
    public Transform transform { get; }   // MonoBehaviour가 이미 제공, 구현체가 별도 작성 불필요
    public void SetWeaponHitActions(SOBulletAction[] _arrHitActions);
}
```

### 🔹 SOBulletAction (구 SOBulletArriveAction)
- `Execute(Bullet _refOwner)` → `Execute(IAttackObject _refOwner)`로 일반화. `SOSpawnExplosionAction`, `SOSpawnRadialDirAction` 등 기존 구현체는 시그니처만 교체.

### 🔹 Bullet.cs
```csharp
[SerializeField] private SOBulletAction[] m_arrArriveActions; // 프리팹 고유, 도착 시
[SerializeField] private SOBulletAction[] m_arrHitActions;    // 프리팹 고유, 명중 시

private SOBulletAction[] m_refWeaponArriveActions; // Weapon이 매 발사마다 덮어씀
private SOBulletAction[] m_refWeaponHitActions;

private void RunHitActions() { RunActions(m_arrHitActions); RunActions(m_refWeaponHitActions); }

public void SetWeaponArriveActions(SOBulletAction[] _arr) => m_refWeaponArriveActions = _arr; // Bullet 전용
public void SetWeaponHitActions(SOBulletAction[] _arr) => m_refWeaponHitActions = _arr;       // 인터페이스 구현
```
`AttackMonster()`에서 `TakeDamage` 직후, 즉 **실제로 데미지를 입힌 순간에만** `RunHitActions()`를 호출 — AliveTime 만료(허공에 사라짐)와 명중을 구분하는 별도 플래그 없이도 구조적으로 "명중 시에만" 실행이 보장됨.

### 🔹 Laser.cs
- `AttackInfo AttackInfo => m_refAttackInfo;` 프로퍼티, `m_refWeaponHitActions` 필드, `RunHitActions()` 추가.
- `AttackMonster()`의 `TakeDamage` 직후 `RunHitActions()` 호출 — Bullet과 동일한 지점에 동일한 방식으로 훅.

### 🔹 Weapon.cs
```csharp
public void GrantArriveAction(SOBulletAction _refAction) => AddGrantedAction(ref m_arrArriveActions, _refAction);
public void GrantHitAction(SOBulletAction _refAction) => AddGrantedAction(ref m_arrHitActions, _refAction);
// Array.Resize는 레벨업 시점(드문 이벤트)에만 발생하므로 GC Alloc 문제 없음

private void ApplyGrantedActions(GameObject _refBulletObj)
{
    IAttackObject refAttackObj = _refBulletObj.GetComponent<IAttackObject>();
    if (refAttackObj == null) return;

    if (m_arrHitActions != null)
        refAttackObj.SetWeaponHitActions(m_arrHitActions);          // Bullet + Laser 공통

    if (m_arrArriveActions != null && refAttackObj is Bullet refBullet)
        refBullet.SetWeaponArriveActions(m_arrArriveActions);       // Bullet 전용
}
```
`Fire()` / `FireCircularSector()` / `FireAndRotate()` 세 발사 경로 모두에서 스폰 직후 `ApplyGrantedActions` 호출.

### 🔹 Player.cs
- 기존 `ModifyWeaponCooldown` 패턴 그대로 `GrantWeaponHitAction(eWeaponType, SOBulletAction)` 패스스루 추가 — `FeatureSO.Apply(Player, level)`에서 호출할 진입점.

**남은 작업**: 레벨업 카드 쪽 `SOFeatureHitCreateBullet`(`SOFeature` 서브클래스) 신규 작성과, 잘못 연결된 `SO_FeatureHitCreateBullet.asset`의 스크립트 재배선은 아직 미착수. `Bullet.AttackMonster()`의 `MaxHitCount` 무시 버그(관통 미동작)도 이번 범위에선 손대지 않음.

## 4. 💡 인사이트 및 요약

> 💡 **핵심 요약**
>
> 1. **Pool 재사용 구조에서 "동적으로 부여되는 능력치"는 항상 Add가 아니라 대입(덮어쓰기)으로 다뤄야 한다**: 같은 인스턴스가 몇 번이고 재사용되기 때문에, 누적형 API는 반드시 중복 실행 버그로 이어진다. 매번 최신 상태로 통째로 덮어쓰면 누적 걱정 없이 항상 정확한 상태를 유지할 수 있다.
> 2. **"언제 실행되는가"는 클래스 계층이 아니라 배열/컬렉션 소속으로 표현하는 게 더 가볍다**: `Execute` 시그니처가 같은 두 종류의 Action을 억지로 별도 상속 트리로 나눌 필요 없이, 같은 베이스를 공유하고 호출부에서 어느 배열에 넣느냐로만 구분하면 재사용성도 늘고 계층도 안 늘어난다.
> 3. **"당연히 상속 관계겠지"라는 가정은 항상 실제 코드로 확인해야 한다**: Bullet과 Laser가 둘 다 `IAttackObject`를 구현한다는 것만 보고 지나쳤다면, Weapon의 `GetComponent<Bullet>()`이 Laser 타입 무기에서 조용히 아무 일도 안 하는 버그를 놓칠 뻔했다. 인터페이스로 계약을 공유하는 두 클래스라도 상속 관계가 아닐 수 있다는 걸 항상 의심해야 한다.

# DEVLOG

## 2026-07-15

### PlayerMovement 부스트 잔여 속도 제거
- **문제**: `MaxRoll`(배럴롤) 시 `ApplySpeedBoost`로 걸리는 롤 방향 가산 속도(`vBoostSpeed`)가 `FixedUpdate`에서 계산만 되고 실제 이동에 반영되지 않았음. 또한 `m_fBoostValue`가 감쇠 후 최소 1.0에서 멈추기 때문에, 그대로 가산에 썼다면 부스트가 끝나도 잔여 속도가 사라지지 않는 구조였음.
- **수정**: `Assets/02_Player/PlayerMovement.cs`
  - `vBoostSpeed`를 `vNewPos` 계산에 실제로 더해서 롤 부스트가 이동에 반영되도록 연결
  - 가산량을 `m_fBoostValue`가 아닌 `(m_fBoostValue - 1.0f)`로 계산해, 부스트가 끝나는 시점(`m_fBoostValue == 1.0f`)에 가산 속도가 정확히 0으로 수렴하도록 변경
  - 기본 이동 배율용 `m_fBoostValue`(최소 1.0 유지)와 가산용 부스트 속도의 역할을 분리

### Bullet 도착(명중/AliveTime 만료) 로직을 SO Action으로 분리
- **배경**: `Missiles`가 `PoolObject.OnPush`에 `SpawnExplosion`을 직접 구독하는 구조였음. 향후 "도착 시 사방으로 총알을 뿌리는" 것처럼 더 복잡한 도착 이벤트가 추가될 경우, 서브클래스별로 구현이 늘어나며 조합이 꼬일 우려가 있어 구조를 논의.
- **결정**: 몬스터 BT의 `SONode` 패턴(SO 기반 Action, 인스펙터에서 조합/교체 가능)을 총알 도착 이벤트에도 동일하게 적용.
- **변경 사항**
  - `Assets/02_Player/Weapon/BulletArriveAction.cs` (신규): `Execute(Bullet _refOwner)`를 갖는 abstract SO 베이스
  - `Assets/02_Player/Weapon/SOSpawnExplosionAction.cs` (신규): 기존 `Missiles.SpawnExplosion` 로직을 이전한 구체 Action
  - `Assets/02_Player/Weapon/Bullet.cs`: `[SerializeField] BulletArriveAction[] m_arrArriveActions` 추가, `OnEnable`에서 `PoolObject.OnPush`에 `RunArriveActions` 공통 구독 → 모든 Bullet 계열이 프리팹 단위로 도착 이벤트를 조합 가능해짐
  - `Assets/02_Player/Weapon/Missiles.cs`: `m_refExplodeObj`, `SpawnExplosion()`, base 호출만 남은 `OnEnable`/`OnDisable` 오버라이드 제거
- **에디터 후속 작업 필요**: `SO_SpawnExplosionAction` 에셋 생성 후 기존 폭발 프리팹 재할당, `Missiles.prefab`의 `Bullet.m_arrArriveActions`에 등록
- **별개로 발견한 이슈**: `Missiles.UpdateDirMissile()`에서 목표 근접 시 조기 반납(`SetAliveTime(0)`)하던 `fArriveDist` 체크 블록이 현재 코드에는 빠져 있어 `fMoveDist`가 미사용 상태이고, 미사일이 타겟을 통과할 수 있음. 이번 작업 범위 밖이라 수정하지 않음.

### BulletArriveAction에서 총알을 직접 스폰하는 문제 해결 (Bullet.SpawnAttackObject 도입)
- **문제**: "도착 시 사방으로 총알을 뿌리는" `SOSpawnRadialDirAction`을 구현하려니, 이 프로젝트 규칙("총알 생성/발사는 Weapon만 담당")과 충돌. Action은 어떤 Weapon 소유로 쐈는지 알 수 없고, Weapon을 매개변수로 억지로 넘기면 Action → Weapon 역참조가 생겨 지금까지 지켜온 단방향 구조(위 → 아래로만 참조)가 깨짐.
- **검토한 대안**
  - A) 풀에서 꺼내 위치/회전 세팅 후 `SetAttack` 호출하는 스폰 로직을 정적 헬퍼로 뽑아 Weapon과 Action이 공통으로 사용
  - B) Action이 직접 `ObjectPool.GetObject` + `SetAttack`을 자체 구현
  - B는 당장은 간단하지만 Weapon.cs와 Action 양쪽에 스폰 로직이 중복되어, "총알 생성은 한 곳에서만" 규칙이 사실상 깨지고 향후 비슷한 Action이 늘수록 중복이 커질 것으로 판단해 A로 진행 결정.
- **변경 사항**
  - `Assets/02_Player/Weapon/Bullet.cs`: `public static GameObject SpawnAttackObject(PoolObject, Vector3, Quaternion, AttackInfo, tShotInfo)` 추가(풀에서 꺼내기 → 위치/회전 세팅 → `SetAttack` 호출을 한 곳에 모음), 외부에서 총알 자신의 공격 정보를 재사용할 수 있도록 `public AttackInfo AttackInfo => m_refAttackInfo` 프로퍼티 추가
  - `Assets/02_Player/Weapon/Weapon.cs`: `Fire()`, `FireCircularSector()`, `FireAndRotate()`가 각자 하던 `GetObject`+`SetAttack`을 전부 `Bullet.SpawnAttackObject` 호출로 교체. 기존 `CreateBullet()`(풀에서 꺼내기 + 발사 이펙트 + 쿨다운 갱신을 함께 처리하던 메서드)은 Weapon 고유 관심사(이펙트 재생, 쿨다운 갱신)만 남긴 `OnBulletFired()`로 축소
  - `Assets/02_Player/Weapon/BulletAction/SOSpawnRadialDirAction.cs`: 피보나치 스피어 분포(`SOFireRadialDirNode`와 동일한 방식)로 사방에 새 공격 오브젝트를 `Bullet.SpawnAttackObject`로 직접 스폰하도록 구현. Weapon 참조 없이 동작.
- **결과**: 총알 생성 책임이 `Bullet.SpawnAttackObject` 한 곳으로 모이고, Weapon과 BulletArriveAction 모두 그 아래로만 의존하는 단방향 구조 유지.

---

# 🚀 [Unity 우주 슈팅] Container 버그 수정 & Feature 슬롯 설명/보유 개수 표시 설계

- **날짜:** 2026-07-20
- **관련 시스템:** UI(Container/SlotView), FeatureManager, Event System

## 1. 🚨 Container.cs 버그 리뷰

코드 리뷰 중 `Container.cs`에서 두 가지 실질적인 결함을 발견.

1. **빌드 시 컴파일 에러**: `ClearData()`(private, 슬롯 오브젝트 파괴용)의 `#else`(비-에디터) 분기에서 존재하지 않는 필드 `m_pContentView`를 참조. `#if UNITY_EDITOR` 분기만 타는 에디터에서는 안 드러나고, 실제 빌드에서만 컴파일이 깨지는 잠복 버그였음.
2. **`SortData()` IndexOutOfRangeException 위험**: 빈 공간 없이 데이터를 앞으로 당기는 로직에서, 대상 슬롯(`i`) 앞쪽에 빈 슬롯이 하나도 없으면 탐색 인덱스 `pre`가 `-1`까지 내려간 채로 `m_listView[pre]`에 접근 → 컨테이너가 꽉 찬 상태에서 `SortData()`를 호출하면 크래시.

### 🛠️ 수정
- `m_pContentView` → `m_refContentView`로 정정.
- `SortData()`에 `if (pre < 0) continue;` 가드 추가 (옮길 빈 슬롯이 없으면 그냥 스킵). 사용되지 않던 루프 내 임시 변수(`iSwapIdx`)도 함께 제거.

## 2. 💡 설계 논의 (대화로 좁혀나간 과정)

**1) 슬롯 선택 시 SO 설명(Description) 텍스트로 보여주기**
- 처음 아이디어: `SODescView` 같은 별도 클래스가 `Container`의 선택 콜백을 구독해서 그려주면 어떨까?
- `Container.OnSelectEvt`가 기존엔 `Action`(파라미터 없음)이라, 구독자가 결과를 알려면 `Container.GetTargetSlot().SOFeat`로 다시 조회해야 했음 → 구독자가 `Container`뿐 아니라 `SlotView` 타입까지 알아야 해서 결합도가 늘어남.
- **결론**: `OnSelectEvt`를 `Action<SOFeature>`로 바꿔 선택된 SO를 이벤트 인자로 직접 push. `SOFeature.m_strDescription`에 `Description` 프로퍼티를 새로 열어주고, 신규 `SODescView`가 이벤트를 구독해 TMP 텍스트에 반영.

**2) 기능 보유 개수(레벨)는 어디서 관리할 것인가**
- 논의 순서:
  1. 1차 제안: `SlotView.Bind()` 시점에 `FeatureManager.GetLevel()`을 조회하고, 이후엔 `SlotView`가 직접 `FeatureManager.OnFeatureAcquired`를 구독해서 자기 갱신.
  2. **반박(사용자)**: 컨테이너에 슬롯이 12개면 흭득 이벤트 1번마다 12개 슬롯이 전부 "내 것 맞나?" 필터링을 반복하게 됨. 실제 성능 임팩트는 거의 없지만(흭득은 프레임마다 도는 이벤트가 아님), 더 근본적인 문제는 **범용 뷰(`SlotView`)가 특정 게임플레이 싱글턴(`FeatureManager`)에 직접 의존하게 된다는 것** — 인벤토리/스킬창 등으로 재사용해야 할 슬롯 뷰가 이 기능 하나 때문에 오염됨.
  3. **재설계**: 구독은 `Container`가 한 번만 하고(슬롯 수와 무관), 필요한 슬롯에만 `SetCount(int)`로 값을 밀어준다. `SlotView`는 `FeatureManager`를 아예 모르게 됨.
- 실제 구현 단계에서 추가로 발견한 함정: `Container.AddData`에 `(SOFeature,int)`/`(SOFeature,int,int=0)` 두 오버로드가 동시에 존재하는 상태라, `FeatureManager.OnFeatureAcquired += AddData` 처럼 메서드 그룹으로 바로 구독하면 두 오버로드 모두 `Action<SOFeature,int>`에 대입 가능해 컴파일러가 어떤 걸 고를지 모호해짐 → 잘못 고르면 이벤트가 준 카운트 값이 조용히 `_iCategoryIdx` 자리로 들어가는 버그가 될 뻔함. 시그니처가 명확한 전용 래퍼 메서드(`OnFeatureAcquired`)를 하나 두고 그 안에서 명시적으로 호출하도록 회피.

## 3. 🛠️ 반영된 핵심 코드 변경 사항

### 🔹 SOFeature.cs
```csharp
public string Description => m_strDescription; // 기존엔 접근자가 없었음
```

### 🔹 Container.cs
```csharp
public event Action<SOFeature> OnSelectEvt; // 기존 Action → 선택된 SO를 직접 전달

private void OnEnable()
{
    if (m_eType == eContainerType.Feature && FeatureManager.m_Instance != null)
        FeatureManager.m_Instance.OnFeatureAcquired += OnFeatureAcquired;
}

// 오버로드 모호성 회피용 래퍼
private void OnFeatureAcquired(SOFeature _refFeature, int _iNewLevel)
{
    AddData(_refFeature, _iNewLevel);
}

// 처음 흭득이면 남는 자리에 추가 + 전체 재바인딩, 이미 있으면 SetSlotCount로 그 슬롯만 갱신
public bool AddData(SOFeature _refSOEntryUI, int _iCount, int _iCategoryIdx = 0) { ... }
```
- `BindData()`에도 스크롤/카테고리 전환으로 슬롯이 새로 바인딩될 때 `FeatureManager.GetLevel()`로 최신 카운트를 반영하는 처리 추가 (레벨업 순간이 아닌 재바인딩 경로 커버).

### 🔹 SlotView.cs
- `BindData()`에서 하던 `FeatureManager.GetLevel()` 직접 조회 제거.
- `private void UpdateCountBadge(int)` → `public void SetCount(int)`로 승격, `Container`가 밀어주는 값만 반영하는 순수 뷰로 축소.

### 🔹 SODescView.cs (신규)
- `Container.OnSelectEvt<SOFeature>`를 구독해 선택된 SO의 `Description`을 TMP 텍스트에 반영. `OnEnable`/`OnDisable`에서 구독/해제.

**남은 작업**: 씬/프리팹에서 `SODescView`에 `Container`와 TMP 텍스트 오브젝트 연결, `Container` 인스펙터에서 `m_eType`을 실제로 `Feature`로 세팅해야 카운트 push 경로가 동작함.

## 4. 💡 인사이트 및 요약

> 💡 **핵심 요약**
>
> 1. **에디터 전용 분기(`#if UNITY_EDITOR`)에 숨은 컴파일 에러는 에디터에서 아무리 테스트해도 안 드러난다.** 실제 빌드 파이프라인을 한 번은 반드시 통과시켜야 하는 이유.
> 2. **"이벤트를 몇 개가 구독하느냐"보다 "누가 무엇에 의존하게 되느냐"가 결합도 판단의 핵심 질문이다.** 12개 슬롯이 매번 이벤트를 필터링하는 성능 비용은 무시할 수준이었지만, 재사용 가능해야 할 뷰 컴포넌트가 특정 매니저 싱글턴을 알게 되는 것은 구조적으로 더 나쁜 문제였음.
> 3. **오버로드가 여러 개인 메서드는 메서드 그룹으로 델리게이트에 바로 구독하지 말 것.** C#은 필요하면 선택적 매개변수를 채워서라도 호환되는 오버로드를 묵시적으로 골라주는데, 후보가 여럿이면 어떤 게 선택될지 코드만 봐서는 확신하기 어렵다. 시그니처가 델리게이트와 정확히 일치하는 전용 래퍼 메서드로 구독하면 이 모호성 자체가 생기지 않는다.

---

# 🚀 [Unity 우주 슈팅] 조커카드 도박 시스템 설계 & SOData 리팩토링 & Container 스크롤/리사이즈 버그 수정

- **날짜:** 2026-07-21
- **관련 시스템:** Feature(레벨업), UI(Container/SlotView), Data Architecture, Object/Card UI

## 1. 🚨 기획 배경 / 문제 상황

- **기획**: 레벨업마다 뜨는 "조커카드" 한 장. 누르면 회차별 확률로 성공/실패가 갈리고, 성공하면 보상 카드를 더 받는다. 연속 성공(스트릭)할수록 다음 성공 확률은 낮아지지만 후보 카드 수·선택 가능 수는 늘어나서, 매 판마다 "더 걸지 vs 지금까지 딴 걸 챙기고 멈출지"를 선택하게 만드는 구조.
- **구조 문제 1**: 기존 `SOFeature`는 고르는 즉시 `FeatureManager`가 레벨을 올리고 스탯을 적용한다. 도박 실패 시 이걸 되돌리려면 `UpAttack`/`UpHP`/`AddWeapon` 등 모든 `SOFeature` 서브클래스에 Revert 로직을 새로 넣어야 해서 "기존 구조를 최대한 깨뜨리지 않는다"는 프로젝트 규칙과 충돌.
- **구조 문제 2**: 뽑은 카드를 보여줄 `Container`/`SlotView`가 처음부터 `SOFeature` 타입에 하드 결합돼 있어, 별개 SO 타입인 조커카드(`SOJokerCard`)를 같은 슬롯 UI에 띄울 방법이 없었음.
- **버그 리뷰**: 조커카드 UI를 얹기 전에 `Container.cs`의 가상 스크롤/리사이즈 로직을 다시 점검하다 4건의 결함을 추가로 발견 (아래 3장 참조).

## 2. 💡 설계 논의 (대화로 좁혀나간 과정)

**1) 도박 실패 시 "몰수"를 어떻게 구현할 것인가**
- 처음 검토: 고를 때마다 바로 `FeatureManager.SelectFeature()`를 호출하고, 실패하면 적용된 스탯을 되돌리는 Revert 로직 추가 → 서브클래스 전수 수정이 필요해 기각.
- **결론**: 선택한 `SOFeature`를 `JokerCardManager`가 `m_listPendingFeature`에 보류만 시켜두고, 실제 `FeatureManager.SelectFeature` 호출은 도박을 끝내고 "현금화"하는 시점에 몰아서 처리. 실패하면 pending 리스트를 비우기만 하면 끝 — 애초에 적용한 적이 없으니 되돌릴 것도 없음. 회차별 성공 확률/후보 수/선택 가능 수는 `SOJokerCard`에 `AnimationCurve` 3개로 데이터만 분리.

**2) Container를 SOFeature 전용으로 유지할지, 공통 부모 타입을 새로 뽑을지**
- 인터페이스(`IDisplayable` 등)로 Icon/Description 계약만 공유하는 방법도 검토했으나, 이러면 `SOFeature`/`SOJokerCard`가 각자 필드를 중복 선언해야 함.
- **결론**: 추상 베이스 SO `SOData`를 신설해 `SOFeature`가 상속하도록 변경. 이 프로젝트가 이미 `SOFeature` 자체를 "추상 베이스 SO 아래 서브클래스" 패턴으로 쓰고 있어서 그 결을 그대로 잇는 쪽이 인터페이스보다 일관적이라고 판단. `Container`/`SlotView`/`ICountable`/`SODescView` 전반의 타입을 `SOFeature` → `SOData`로 일반화.
- **트레이드오프**: `ICountable.GetCount`가 `SOData`를 받게 되면서 `FeatureManager.GetCount`는 `is not SOFeature`로 한 번 다운캐스트가 필요해짐. 인스펙터에서 MonoBehaviour를 캐스팅해 꽂는 기존 `ICountable` 패턴상, 제네릭 인터페이스보다 이쪽이 Unity스럽다고 보고 그대로 채택.

**3) 조커카드 트리거를 CardCreator에 어떻게 얹을 것인가**
- `RandomFeatureCard.Setup()`은 원래도 `.Icon`만 참조했기 때문에 `SOFeature` 전용일 이유가 없었음 → `SOData` 기반으로 일반화해서 회전 연출/아이콘 스왑 뷰를 조커카드에도 그대로 재사용.
- 다만 `CardCreator.HandleCardClicked` 안에서 타입 분기(`is SOFeature` / `is SOJokerCard`)로 합치는 안은 기각. 기능카드는 "1장 클릭 = 즉시 확정"이고 조커카드 후보는 "K장까지 토글 후 별도 확정"이라 클릭 한 번의 의미 자체가 다름 — 한 메서드에 분기를 쌓는 대신 `m_refJokerCard` 전용 슬롯을 따로 두고 `HandleJokerCardClicked`를 별도 이벤트로 구독.

## 3. 🛠️ 반영된 핵심 코드 변경 사항

### 🔹 SOData.cs (신규)
```csharp
public abstract class SOData : ScriptableObject
{
    [TextArea]
    [SerializeField] private string m_strDescription;
    public string Description => m_strDescription;

    [SerializeField] private Sprite m_sprIcon;
    public Sprite Icon => m_sprIcon;
}
```
`SOFeature : SOData`, `SOJokerCard : SOData`로 변경하고 각자 중복 선언하던 Description/Icon 필드는 제거. 기존 SOFeature 에셋의 필드 값은 Unity가 이름 기준으로 직렬화하므로 클래스 계층을 옮겨도 유지됨(마이그레이션 후 인스펙터에서 육안 확인 권장).

### 🔹 Container.cs / SlotView.cs / ICountable.cs / SODescView.cs
- `CategoryData.ListData`, `Container.OnSelectEvt`, `AddData`/`DeleteData`/`GetListData`/`GetDataIdx`, `SlotView.SOFeat`/`Bind`/`BindData`, `ICountable.GetCount`, `SODescView.OnSelect` 전부 `SOFeature` → `SOData`로 교체.
- `FeatureManager.GetCount(SOData)`는 `is not SOFeature` 가드로 다운캐스트.

### 🔹 Container.cs — 스크롤/리사이즈 버그 4건
1. **`Resize()` 확장 분기 누락**: 축소(`listData.Count > _iCount`)만 구현돼 있고 늘리는 경우가 아예 없어서, 용량을 늘리는 호출이 조용히 아무 일도 안 했음 → `else if (listData.Count < _iCount)` 분기로 `null` 채워 넣는 코드 추가.
2. **`Sort()`의 category 0 하드코딩**: 함수 초반엔 `GetCategoryData(m_iCurrentCategoryIdx)`로 현재 카테고리를 제대로 가져오면서도, 행 개수 클램프와 스크롤 가능 높이(`m_vContaninerSize.y`) 계산은 `m_listCategoryData[0]`을 참조 → 카테고리가 2개 이상이면 1번 이후 카테고리는 항상 0번 데이터 크기 기준으로 스크롤 범위가 계산되던 버그. 전부 현재 카테고리(`listData`) 참조로 교체.
3. **슬롯 flat 인덱스 공식 오류**: 슬롯 생성 루프의 종료 조건이 `i * j + j >= listData.Count`로 돼있었는데, 이는 `i`행 `j`열의 실제 flat 인덱스(`i * m_iColCount + j`)와 다른 식이라 `BindData()`의 인덱싱 방식과 불일치 — 행이 늘어날수록 생성되는 슬롯 개수가 실제 데이터 개수와 어긋남. `i * m_iColCount + j`로 수정.
4. **`Resize()` 축소 시 이벤트 누락**: `RemoveRange`로 잘라내는 뒤쪽 슬롯에 실제 데이터가 남아있어도 `OnDeleteEvt`가 안 불려서 다른 시스템이 그 삭제를 알 방법이 없었음 → 삭제 전 non-null 항목에 대해 `OnDeleteEvt?.Invoke(...)` 호출 추가.

```csharp
// Container.cs — Sort() 슬롯 생성 루프, 수정 후
for (int i = 0; i < m_iRowCount; ++i)
    for (int j = 0; j < m_iColCount; ++j)
    {
        if (i * m_iColCount + j >= listData.Count)
            break;
        // ...슬롯 인스턴스화
    }
```

### 🔹 RandomFeatureCard.cs / CardCreator.cs
- `RandomFeatureCard`: `m_SOFeature`/`SOFeature`/`OnCardClicked` → `m_SOData`/`Data`/`Action<SOData>`로 일반화.
- `CardCreator`: `m_refJokerCard`(전용 슬롯) + `m_SOJokerCard`(표시 데이터) 추가, `HandleJokerCardClicked`를 별도 이벤트 구독으로 분리. `HandleCardClicked`는 `is not SOFeature` 가드 추가.

**남은 작업**: `CardCreator.HandleJokerCardClicked`의 `TryGamble` 성공 분기가 아직 TODO 스텁. 성공 후 후보 카드를 K-of-N 토글로 선택하고 확정/현금화하는 UI 흐름은 다음 작업으로 남음.

## 4. 💡 인사이트 및 요약

> 💡 **핵심 요약**
>
> 1. **"되돌리기 어려운 부수효과"가 있는 로직은, 실패 가능성이 있는 흐름 앞에서 아예 적용을 미루는 편이 Revert 로직을 새로 만드는 것보다 싸다.** 조커카드 몰수를 "이미 적용된 스탯을 되돌리기"가 아니라 "적용을 pending 리스트로 미뤘다가 확정 시점에만 실제로 적용하기"로 설계하니, 실패 처리가 리스트 Clear 한 줄로 끝났다.
> 2. **여러 도메인 SO가 같은 범용 UI(Container/SlotView)를 공유해야 한다면, 인터페이스보다 프로젝트가 이미 쓰고 있는 상속 패턴을 따라가는 쪽이 결이 맞는다.** `SOFeature`가 이미 "추상 베이스 SO + 서브클래스" 구조였기 때문에, 공통 부모(`SOData`)를 뽑아 그 위에 얹는 게 인터페이스로 계약만 공유하는 것보다 필드 중복도 없고 기존 컨벤션과도 일치했다.
> 3. **"클릭 한 번"의 의미가 도메인마다 다르면, 한 핸들러에 타입 분기를 쌓기보다 이벤트 구독 자체를 분리하는 게 낫다.** 기능카드(즉시 확정)와 조커카드 후보(토글 후 확정)는 겉보기엔 같은 "카드 클릭"이지만 선택 상태 머신이 완전히 다르므로, 뷰 컴포넌트(`RandomFeatureCard`)는 공유하되 컨트롤러의 이벤트 구독은 도메인별로 나누는 편이 조건문이 쌓이는 것보다 안전했다.
> 4. **컨테이너 유틸리티 코드에서 "카테고리 0번"처럼 특정 인덱스를 하드코딩하는 부분은 리팩토링/버그 스캔 시 우선적으로 의심할 지점이다.** 함수 초반에 현재 카테고리를 제대로 가져와놓고도 후반부 계산에서 다른 변수(`m_listCategoryData[0]`)를 실수로 쓰는 패턴은, 카테고리가 1개뿐인 동안은 절대 드러나지 않다가 카테고리를 늘리는 순간 조용히 터지는 전형적인 잠복 버그였다.

---

# 🚀 [Unity 우주 슈팅] 씬 전환형 오브젝트 풀 로딩 — Addressables + UniTask 비동기 프리워밍

- **날짜:** 2026-07-23
- **관련 시스템:** Object Pool, Scene Loading, Addressables, Async(UniTask)

## 1. 🚨 문제 상황

- 기존 `ObjectPool`은 인스펙터에 직렬화된 `List<PoolInfo>`(프리팹 + 개수)를 `Start()`에서 전부 동기 `Instantiate`하는 구조. `MainScene.unity`에 이미 21개 항목(최대 800개짜리 포함)이 박혀 있어 초반 로딩이 길고, 프레임 한 번에 몰아서 생성되니 스파이크도 발생.
- "총알/파티클/몬스터를 로드한 만큼 거의 다 소모하는 사용 패턴이라 Addressable이 필요 없지 않나?"라는 질문에서 출발 — 메모리에 다 올려놓고 거의 다 쓰는 상황이면 Addressable의 핵심 이점(안 쓰는 걸 내려서 절약)이 안 산다는 판단.
- 다만 이어진 확인 과정에서 **스테이지마다 몬스터 풀 구성을 거의 다 갈아치운다**는 사실이 드러남 — 이 경우 게임 전체 기준으로는 에셋 종류가 많은데 한 씬에서 쓰는 건 일부라, Addressable로 스테이지 전환 시 이전 것을 실제로 언로드하는 이점이 다시 성립.

## 2. 💡 설계 논의 (대화로 좁혀나간 과정)

**1) Addressables 전환 범위**
- 처음엔 `SOPoolData` 하나가 `List<AssetReferenceGameObject>`를 통째로 들고 있는 안도 검토했으나, 항목별로 `PreLoad`/`Max`를 따로 못 주는 구조라 기각.
- **결론**: `SOPoolData` = 프리팹 1종(`AssetReferenceGameObject`) + `PreLoad`/`Max`, `SOSceneData` = `List<SOPoolData>`. 스테이지 진입 시 `SceneController`가 그 씬에 필요한 `SOSceneData` 하나를 `PoolManager`(`ObjectPool`)에 넘기는 구조로 확정.
- **추가로 확인한 함정**: `Bullet.cs`, `Laser.cs`, `SOAttackInfo.cs` 등 8곳이 이미 `PoolObject`를 **direct 하드 레퍼런스**로 들고 있음을 발견 — 이런 곳까지 Addressable로 안 바꾸면 그 프리팹들은 하드 레퍼런스 때문에 메모리에 그대로 남아 언로드 이점이 없음. 다만 그 대상들(플레이어 무기 히트이펙트 등)은 스테이지 공통으로 계속 쓰는 에셋이라 애초에 언로드할 필요가 없다고 보고, **1차 범위는 `SOPoolData`/`SOSceneData`(스테이지별로 실제로 갈아치우는 몬스터 계열 풀)로 한정**. 나머지 direct 참조 8곳은 이번 범위에서 변경하지 않음.

**2) 초반 로딩 속도 — UniTask 채택 근거**
- "로드하고 씬에 올리는 작업을 순수 `Task`/`Thread`로 나누면 되지 않나?"라는 질문에 대해: `Instantiate`/`GetComponent` 등 Unity API는 메인 스레드 전용이라 워커 스레드에서 직접 다루면 깨짐. `UniTask`는 이미 프로젝트에 플러그인으로 포함돼 있고(`Assets/Plugins/UniTask`), `PlayerLoop`에 편입되어 `await`가 메인 스레드로 자연스럽게 복귀하며, Addressables 확장(`AsyncOperationHandle.ToUniTask()`)까지 지원해 이 케이스에 정확히 맞는다고 판단.
- 여러 `SOPoolData`의 로딩을 순차가 아닌 `UniTask.WhenAll`로 묶어 프레임 양보 지점에서 인터리빙되게 하고, `Instantiate`는 4개당 한 번 `UniTask.Yield`로 프레임 분산해 스파이크 방지.
- 대화 말미에 Unity 2022.3.20+ 정식 지원되는 `Object.InstantiateAsync<T>(prefab, count)`(내부적으로 Job System으로 일부 병렬 처리 후 메인 스레드에서 마무리, UniTask 확장 `AsyncInstantiateOperation<T>.ToUniTask()`도 이미 존재)가 수동 프레임 분산 루프보다 나은 대안이라는 데까지 논의 진행 — **아직 코드에는 미반영, 다음 작업으로 남음**.


---------------------------------------------------------------------
                            이후 변경된 점
---------------------------------------------------------------------

- 여러 `SOPoolData`의 로딩을 순차가 아닌 `UniTask.WhenAll`로 묶어 프레임 양보 지점에서 인터리빙되게 하고, 처음엔 `Instantiate`를 4개당 한 번 `UniTask.Yield`로 프레임 분산해 스파이크를 방지하는 수동 루프로 구현.
- 이 수동 루프는 "Instantiate를 개별 호출로 너무 많이 하는것 아니냐"는 지적을 받고 갱신됨: Unity 2022.3.20+ 정식 지원되는 `Object.InstantiateAsync<T>(prefab, count)`는 동일 프리팹 N개를 한 번에 요청하면 내부적으로 Job System이 컴포넌트 데이터 복사를 병렬 처리하고 메인 스레드에서 마무리해주는
 배치 API(UniTask 확장 `AsyncInstantiateOperation<T>.ToUniTask()`도 이미 존재). 대화 초반엔 "논의만 하고 다음 작업으로미루자"로 기록했었지만, 같은 세션 안에서 바로 적용 가능하
다고 판단해 수동 프레임 분산 루프와 그 상수(`c_iInstantiatePerFrame`)를 지우고 배치 호출 한 줄로 교체 — 남겨뒀던 "미반영" 기록을 실제 반영 상태로 갱신.

**3) SO 오염 함정 — `AssetReference.LoadAssetAsync()`를 직 접 호출하면 안 되는 이유** - 구현 중 "`_refData.PrefabRef.LoadAssetAsync()`로 SO에 직접 접근하면 SO 자체가 오염되는 거 아니냐"는 지적이 나와 Addressables 소스(`AssetReference.cs`)를 확인.
- `AssetReference.LoadAssetAsync()`는 로드한 핸들을 **자기
자신의 내부 필드(`m_Operation`)에 저장**하는 구조이고, 공식 주석에도 "이미 로드된 상태에서 다시 호출하면 안 되며, 여러번 로드하려면 `Addressables.LoadAssetAsync(object)`에 AssetReference를 키로 넘기라"고 명시돼 있음. `SOPoolData`는 여러 `SOSceneData`(스테이지)가 공유할 수 있는 영속 에셋이라,
`AssetReference` 자체의 상태를 건드리면 두 스테이지가 같은`SOPoolData`를 참조할 때 두 번째 로드 시도가 에러를 내며 조용히 깨지는 구조 — CLAUDE.md가 금지하는 "SO 데이터 오염"과동일한 문제.
- **해결**: `Addressables.LoadAssetAsync<GameObject>(_refData.PrefabRef)`로 AssetReference를 순수 키로만 사용하고, 반환된 `AsyncOperationHandle`은 `ObjectPool` 쪽 `Dictionary<PoolObject, AsyncOperationHandle>`에 직접 보관.
해제도 SO의`ReleaseAsset()`이 아니라 우리가 들고 있는 핸들을 `Addressables.Release()`로 직접 해제 — SO는 순수 데이터로 유지되고,여러 스테이지가 공유해도 각자 독립적인 참조 카운트를 가짐.

## 3. 🛠️ 반영된 핵심 코드 변경 사항

### 🔹 SOPoolData.cs
- 기존 코드가 `[CreateAssetMenu]`인데 `MonoBehaviour`를 상속해 SO 에셋 생성이 안 되던 버그를 `ScriptableObject` 상속으로 수정.
```csharp
[CreateAssetMenu(fileName = "SO_PoolData", menuName = "Game/Load/PoolData")]
public class SOPoolData : ScriptableObject
{
    public AssetReferenceGameObject PrefabRef;
    public int PreLoad = 8;
    public int Max = 12; // 아직 사용하지 않음(동적 증설 미구현)
}
```

### 🔹 SOSceneData.cs (신규)
```csharp
[CreateAssetMenu(fileName = "SO_SceneData", menuName = "Game/Load/SceneData")]
public class SOSceneData : ScriptableObject
{
    public List<SOPoolData> PoolDataList = new List<SOPoolData>();
}
```

### 🔹 ObjectPool.cs
- 인스펙터 직렬화 `List<PoolInfo>` + `Start()` 동기 프리로드 제거.
- `BuildPoolsAsync(List<SOPoolData>, CancellationToken)`: 이전 풀 정리 후 `SOPoolData`별 `AsyncLoad`를 `UniTask.WhenAll`로 동시 진행.
- `AsyncLoad`: `AssetReferenceGameObject.LoadAssetAsync().ToUniTask(...)`로 프리팹 로드 → `PreLoad`개 `Instantiate`를 프레임 분산.
- `ClearAllPools()`: 큐에 남은 인스턴스 `Destroy` + 로드했던 `AssetReferenceGameObject.ReleaseAsset()`로 실제 메모리 해제 — 스테이지 전환 시 이전 스테이지 몬스터 풀이 실제로 언로드되는 지점.
- `GetObject`/`PushObject`/`GetObjectCount`/`m_Instance`는 시그니처 변경 없이 유지해 기존 8곳 호출부는 무수정.

### 🔹 SceneController.cs
```csharp
[SerializeField] private SOSceneData m_refSceneData;

private async UniTaskVoid Start()
{
    if (m_refSceneData == null) { Debug.Log("씬 데이터 미설정 : SceneController"); return; }
    await ObjectPool.m_Instance.BuildPoolsAsync(m_refSceneData.PoolDataList, this.GetCancellationTokenOnDestroy());
}
```

**남은 작업**:
- `MainScene.unity`의 기존 `ObjectPool` 인스펙터 21개 항목(합계 다수, 최대 800개)이 필드 제거로 못 쓰게 됨 — 각 프리팹 Addressable 마킹 + 동일 수치로 `SOPoolData` 에셋 21개 재구성, `SOSceneData`에 모아서 `SceneController`에 할당 필요 (에디터 작업, 스크립트로 대체 불가).
- 스테이지별로 별도 `SOSceneData` 에셋 구성 필요.
- `Object.InstantiateAsync` 도입 검토 중, 미반영.
- `SOPoolData.Max`(풀 상한) 필드는 선언만 되어 있고 동적 증설 로직에는 아직 연결되지 않음.

## 4. 💡 인사이트 및 요약

> 💡 **핵심 요약**
>
> 1. **"로드한 걸 거의 다 쓴다"는 사실만으로 Addressable 필요성을 판단하면 안 된다.** 판단 기준은 "한 씬 안에서의 사용률"이 아니라 "게임 전체 기준으로 안 쓰는 걸 실제로 내릴 수 있는가"다. 스테이지마다 몬스터 풀을 거의 다 갈아치우는 이 프로젝트는 씬 내 사용률은 높아도, 스테이지 전환 시점엔 이전 걸 언로드할 여지가 크다.
> 2. **일부만 Addressable로 바꾸면 하드 레퍼런스가 있는 다른 곳 때문에 언로드 이점이 무효화될 수 있다.** `Bullet`/`Laser`/`SOAttackInfo`가 같은 프리팹을 direct로 물고 있으면, `SOPoolData`만 `AssetReference`로 바꿔도 그 프리팹은 여전히 메모리에 상주한다. 전환 범위는 "실제로 스테이지마다 갈아치우는 대상"으로 한정하는 게 비용 대비 합리적이었다.
> 3. **Unity API는 메인 스레드 전용이라, 로딩/인스턴스화의 "속도"는 스레드 분리가 아니라 프레임 분산과 Unity 자체의 비동기 API(`InstantiateAsync`, Addressables `LoadAssetAsync`)로 얻어야 한다.** `UniTask`는 이 둘을 `PlayerLoop`/Addressables 확장으로 자연스럽게 이어주는 접착제 역할이지, 그 자체가 멀티스레드 인스턴스화를 가능하게 하는 도구는 아니다.

---

# 🚀 [Unity 우주 슈팅] DungeonManager — PQ 기반 시간 예약 스포너

- **날짜:** 2026-07-25
- **관련 시스템:** Dungeon/Spawner, Object Pool, Optimization

## 1. 🚨 목표

- 던전 진행 중 "몇 초 뒤 이 오브젝트를 이 위치에 스폰"처럼 시간 기반 예약이 여러 건 동시에 쌓이는 상황을, 매번 정렬하지 않고 항상 가장 빠른 예약만 O(log n)으로 확인/처리하고 싶었음.
- `Assets/14_Uti/PriorityQueue.cs` (이진 힙 기반 범용 우선순위 큐)를 신설하고, `DungeonManager`가 이를 스폰 예약 큐로 사용하도록 설계.

## 2. 💡 설계 논의 (대화로 좁혀나간 과정)

**1) 대기 방식: "정확히 그 시간만큼 잠들기" vs "매 프레임 최상단만 감시하기"**
- 1차 제안: PQ 최상단 항목의 예약 시각까지 `UniTask.Delay`로 정확히 그만큼 슬립한 뒤 `Dequeue`. 시간 계산은 정확하지만, 중간에 그보다 더 이른 예약이 새로 들어와도 이미 시작된 `Delay`를 취소하기 전까진 반영이 안 되는 구조적 약점이 있음.
- **결론(사용자 지정)**: "시간을 예약해두면 별도로 계속 지켜보는 놈이 있다"는 개념으로 변경 — 매 프레임 PQ 최상단만 `Peek`해서 예약 시각이 지났는지 확인하고, 지났으면 그때 `Dequeue`+스폰. 새 예약이 중간에 들어와도 다음 프레임에 자동으로 반영되어 별도 취소/재시작 로직이 필요 없어짐.

**2) 감시 루프를 `Update()`로 만들지 않은 이유**
- 실행 빈도(매 프레임 1회)는 `Update()`와 동일하지만, `Update()`는 오브젝트가 살아있는 한 Unity 엔진이 무조건 호출하는 콜백이라 MonoBehaviour당 디스패치 오버헤드가 붙고, 정지/재개를 직접 관리해야 함.
- `UniTaskVoid` + `while(true) { ... await UniTask.Yield(_tToken); }` 형태로 `PlayerLoop`에 직접 붙이면 같은 프레임 주기를 얻으면서도, `this.GetCancellationTokenOnDestroy()` 토큰이 취소되는 순간 `await` 지점에서 바로 `OperationCanceledException`이 발생해 루프가 종료됨 — `OnDestroy`에서 별도 정리 코드 없이 CLAUDE.md의 "Update 사용 최소화" 원칙을 지킴.

**3) 스폰 대상 참조 타입: `PoolObject` 직접 참조 vs `AssetReferenceGameObject`**
- `SOPoolData.PrefabRef`(AssetReferenceGameObject)는 `ObjectPool.LoadPoolAsync`가 씬 시작 시 Addressables로 **비동기 로드**할 때만 쓰는 참조.
- 반면 `Bullet.m_refHitEffectObj`, `SOSpawnAttackObject.m_refAttackObjectPrefab`처럼 이미 로드되어 풀에 들어간 오브젝트를 `ObjectPool.GetObject(PoolObject)`로 꺼낼 때 쓰는 참조는 단순 **딕셔너리 키**라서 로딩 비용이 없어야 함 — 명중/도착처럼 프레임 중 빈번히 도는 경로이기 때문.
- `DungeonManager`의 스폰 예약도 같은 성격(이미 풀에 있는 대상 중 뭘 꺼낼지 가리킴)이라 `PoolObject`를 직접 들고 있는 기존 방식을 그대로 따름. `AssetReferenceGameObject`로 바꾸면 스폰마다 불필요한 간접비용만 늘어남.

## 3. 🛠️ 구현 및 리뷰에서 발견한 버그

1차 구현 직후 코드 리뷰에서 실제로 동작을 깨뜨리는 문제 4가지를 발견해 함께 수정.

| 문제 | 증상 | 수정 |
| --- | --- | --- |
| `UpdateSpawnObject`를 아무도 호출하지 않음 | 감시 루프 자체가 실행 안 됨, 예약해도 영원히 스폰 안 됨 | `Start()`에서 `UpdateSpawnObject(this.GetCancellationTokenOnDestroy()).Forget()` 호출 추가 |
| 루프 안에 `Dequeue()` 누락 | `Peek()`만 하고 스폰해서 같은 항목이 매 프레임 무한 반복 스폰, 다른 예약은 영원히 순서가 안 옴 | 스폰 직전 `m_PQObject.Dequeue()` 추가 |
| `await UniTask.Yield()` 뒤 `continue` 누락 | 큐가 비었을 때 한 프레임 쉬고도 바로 `Peek()`으로 떨어져 `InvalidOperationException` 위험, "아직 시간 안 됨" 분기도 한 프레임만 쉬고 그대로 스폰돼버려 시간 비교가 사실상 죽은 코드 | 두 분기 모두 `await` 뒤 `continue` 추가 |
| `SpawnObject`에서 null 체크 없음 | 풀이 비어 `ObjectPool.GetObject`가 `null`을 반환하면 `NullReferenceException` | `refGameObject == null` 가드 추가 |

### 최종 구조 (`Assets/05_Manager/DungeonManager.cs`)
```csharp
private struct tSpawnData
{
    public float fSpawnTime;
    public PoolObject refSpawnObject;
    public Vector3 vPosition;
}
private struct tSpawnTimeComparer : IComparer<tSpawnData>
{
    public int Compare(tSpawnData x, tSpawnData y) => x.fSpawnTime.CompareTo(y.fSpawnTime);
}

PriorityQueue<tSpawnData> m_PQObject;

private void Awake() => m_PQObject = new PriorityQueue<tSpawnData>(new tSpawnTimeComparer());
private void Start() => UpdateSpawnObject(this.GetCancellationTokenOnDestroy()).Forget();

private async UniTaskVoid UpdateSpawnObject(CancellationToken _tToken)
{
    while (true)
    {
        if (m_PQObject.Count <= 0) { await UniTask.Yield(_tToken); continue; }

        var tSpawn = m_PQObject.Peek();
        if (tSpawn.fSpawnTime - Time.time > 0.0f) { await UniTask.Yield(_tToken); continue; }

        m_PQObject.Dequeue();
        SpawnObject(tSpawn.refSpawnObject, tSpawn.vPosition);
    }
}

public void AddSpawnObject(float _fNextSpawnTime, PoolObject _refPoolData, Vector3 _vPosition = default)
{
    m_PQObject.Enqueue(new tSpawnData(Time.time + _fNextSpawnTime, _refPoolData, _vPosition));
}

private void SpawnObject(PoolObject _refSpawnObject, Vector3 _vPosition)
{
    GameObject refGameObject = ObjectPool.m_Instance.GetObject(_refSpawnObject);
    if (refGameObject == null) return;
    refGameObject.transform.position = _vPosition;
}
```

**남은 작업**: 실제 던전 웨이브/스폰 테이블에서 `AddSpawnObject`를 호출하는 쪽은 아직 미착수 (지금은 예약→감시→스폰 파이프라인만 완성된 상태). `DungeonManager`를 어디가 들고 있을지(예: `SceneManager`가 소유)는 별도 논의 중.

## 4. 💡 인사이트 및 요약

> 💡 **핵심 요약**
>
> 1. **"매 프레임 확인해야 한다"가 곧 `Update()`를 써야 한다는 뜻은 아니다.** `UniTaskVoid` + `Yield` 루프는 같은 프레임 주기를 얻으면서도 MonoBehaviour 콜백 오버헤드 없이 `CancellationToken`으로 생명주기까지 자동 정리된다.
> 2. **PQ + 감시 루프 조합에서 `Peek`만 하고 `Dequeue`를 빠뜨리면 같은 항목이 무한 반복되는 게 전형적인 함정이다.** "확인"과 "제거"는 항상 짝을 맞춰 리뷰해야 한다.
> 3. **`await` 뒤 조건 분기에서 `continue`(또는 `return`)를 빠뜨리면, 대기 자체는 코드상 존재하지만 실행 흐름은 대기 결과와 무관하게 그대로 다음 줄로 떨어진다.** "한 프레임 쉬고 다시 조건을 검사한다"와 "한 프레임 쉬고 무조건 진행한다"는 완전히 다른 동작이므로, 비동기 폴링 루프를 작성할 때는 각 분기의 탈출 경로를 명시적으로 그려봐야 한다.
> 4. **참조 타입(`AssetReference` vs 직접 참조)은 "로딩이 필요한 지점"과 "이미 로드된 것 중 무엇을 쓸지 가리키는 지점"을 구분해서 선택해야 한다.** 후자에 전자를 쓰면 매번 불필요한 간접비용이 붙는다.

---

# 🚀 [Unity 우주 슈팅] DungeonManager 구조 확정 — Stage 소유권, 몬스터 풀링 연동

- **날짜:** 2026-07-26
- **관련 시스템:** Dungeon/Spawner, Object Pool, Monster, Optimization

## 1. 🚨 오늘 정리한 것

지난 논의에서 미뤄뒀던 것들을 확정: **(1)** `ObjectSpawner`(구 `DungeonManager`, 예약→감시→스폰 엔진)와 스테이지 진행 규칙을 누가 들고 있을지, **(2)** 몬스터가 실제로 Object Pool을 타게 되면서 생기는 부작용.

## 2. 💡 설계 논의

**1) `DungeonManager`(신규) vs `ObjectSpawner`(기존 예약 엔진) 분리**
- 기존에 만들어뒀던 시간 예약 감시 루프는 이미 사용자가 `ObjectSpawner`로 이름을 바꿔서 `Assets/14_Uti/ObjectSpawner.cs`에 정리해둔 상태였음 — "몬스터/스테이지/보스" 같은 게임 규칙은 전혀 모르는 순수 엔진으로 유지.
- 그 위에 스테이지 순서 진행 + 보스 트리거 + 던전 클리어 판정을 담당하는 `DungeonManager`(신규, 유일한 싱글턴)를 별도로 두고, `ObjectSpawner`를 `[SerializeField]`로 소유하게 함. `List<SOStage>`도 `DungeonManager`가 순서대로 진행.
- 이렇게 나눈 이유: `ObjectSpawner`는 아이템 드랍 타이머 등 다른 용도로도 재사용 가능한 범용 유틸로 남기고, "몬스터 다 죽으면 보스" 같은 게임 규칙은 전부 `DungeonManager` 한 곳에 모아 진입점을 하나로 고정(다른 매니저들처럼 `X.m_Instance`로만 접근).

**2) 몬스터가 이제 진짜로 Object Pool을 탐**
- `ObjectSpawner`가 몬스터를 `ObjectPool.GetObject(PoolObject)`로 꺼내 쓰게 되면서, `Monster.cs`에 예전부터 남아있던 `//TODO 몬스터 Object Pool 도입 시 PushObject로 교체` 코멘트가 더 이상 미룰 수 없는 실제 버그가 됨 — `Dead()`가 `SetActive(false)`만 하고 있어서 풀 큐에 반납이 안 되고, 그대로 두면 프리로드된 몬스터가 하나씩 소모되다 고갈됨.
- `Bullet.cs`가 `PoolObject.OnPush`를 구독해 도착 액션을 실행하는 것과 같은 패턴으로, `Monster`도 `OnPush`를 구독해 재사용 시 새는 상태(넉백 코루틴, 상태이상 비트마스크)를 정리하도록 맞춤. `OnEnable()`의 State/Speed/HP 리셋만으로는 이 두 가지가 안 지워졌음.

## 3. 🛠️ 반영된 핵심 코드 변경 사항

### 🔹 Monster.cs
```csharp
private void OnEnable()
{
    m_refBlackBoard.ObjInfo.State = eEntityState.Idle;
    m_refBlackBoard.ObjInfo.Speed = m_SOMonsterInfo.MaxSpeed;
    m_refBlackBoard.ObjInfo.CurrentHP = m_SOMonsterInfo.MaxHP;

    if (m_refPoolObj != null)
        m_refPoolObj.OnPush += ResetPoolState;
}

// 풀 반납 시점(OnPush)에 실행 — "이전 생"의 넉백 코루틴/상태이상 비트마스크 정리
private void ResetPoolState()
{
    if (m_CoNockback != null) { StopCoroutine(m_CoNockback); m_CoNockback = null; }
    m_refBlackBoard.ObjInfo.CurrentEffects = 0;
}
```
`Effects[]` 배열의 `EndTime`/`TickDamage` 자체는 안 지워도 되는데, `SOPassiveEffectNode`/`CheckStateEffect` 등 모든 읽기 경로가 `CurrentEffects` 비트를 먼저 확인하고서야 그 값을 보기 때문. `Dead()`도 `gameObject.SetActive(false)` → `ObjectPool.m_Instance.PushObject(gameObject)`로 교체(풀 컴포넌트가 없으면 기존 방식으로 폴백).

### 🔹 DungeonManager.cs (신규) / SOStage.cs (신규)
- `SOStage`: 스테이지별 몬스터 스폰 테이블(`fSpawnTime`, `MonsterPrefab`, `vPosition`)과 보스 정보만 담는 순수 데이터, `SOPoolData`/`SOSceneData`와 동일하게 별도 SO 에셋으로 분리.
- `DungeonManager`: `StartStage`로 그 스테이지의 예약을 전부 `ObjectSpawner.AddSpawnObject`에 흘려보내고, `Monster.OnMonsterDied` 구독으로 처치 카운트다운 → 조건 충족 시 보스 등장 → 보스 사망 시 다음 스테이지로 진행.

## 4. 🧭 아직 결론 안 낸 것 (TODO로 이관)

- **`DungeonManager`를 로비 씬에 두기로 결정** (로비에서 스테이지를 고르고 그대로 던전 씬까지 들고 감). 그런데 `SOStage.SpawnEntry.MonsterPrefab`이 `PoolObject` 하드 레퍼런스라서, 로비 진입 시점부터 전체 스테이지의 몬스터 프리팹이 메모리에 올라오는 문제가 그대로 남음.
- 대안(선택과 실행 분리 / `AssetReferenceGameObject`+GUID 키 전환)까지 논의했지만, 우선순위상 **지금은 이 상태로 진행하고 리팩토링은 나중으로 미룸** — `Docs/TODO.md`에 두 후보안과 함께 상세 기록.
- 부수적으로 확인된 것: `AssetReferenceGameObject`는 `Equals`/`GetHashCode`를 오버라이드하지 않아 참조 동일성 비교라 그대로 Dictionary 키로 못 씀(같은 에셋도 다른 인스턴스면 조회 실패) → 나중에 이 리팩토링을 하게 되면 `AssetGUID`(string) 혹은 이를 파싱한 `System.Guid`를 키로 써야 함.

**다음 작업**: 일단 로비 씬 UI/흐름부터 만들고, 그 위에서 던전 진입 연결을 이어서 진행하기로 함.

---

# 🚀 [Unity 우주 슈팅] SelectStage(로비 UI) + Addressable 씬 비동기 로딩/진행률 표시

- **날짜:** 2026-07-26
- **관련 시스템:** UI, Scene Loading, Addressables, Object Pool, Async(UniTask)

## 1. 🚨 오늘 정리한 것

바로 전 항목("DungeonManager 구조 확정")의 다음 작업이었던 **로비 씬 UI/흐름**을 실제로 붙임: 스테이지 선택 화면(`SelectStage`)에서 이미지를 클릭하면 Addressable로 등록된 게임 씬을 비동기로 로드하고, 그 진행률을 이미지 하나로 보여준다.

## 2. 💡 설계 논의

**1) SelectStage — RectMask2D 캐러셀**
- `RectMask2D`로 잘린 콘텐츠 안에 스테이지 이미지(`BaseButtonUI`)를 가로로 나열, 이전/다음 버튼을 누르면 `DOTween`으로 오프셋 500만큼 `DOAnchorPosX` 이동 (트윈 도중 중복 입력 방지, 첫/마지막 페이지에서 클램프).
- 각 이미지는 `for` 루프에서 델리게이트를 등록하는데, 루프 변수를 그대로 캡처하면 전부 마지막 값을 참조하는 클로저 버그가 나서 로컬 변수로 복사 후 캡처. `OnDestroy`에서 등록한 델리게이트를 그대로 구독 해제.

**2) SOSceneData/GameSceneManager 소유권 — "누가 List를 들고 있나"**
- 처음엔 `SOSceneData`가 `List<AssetReference>`(씬 여러 개 합쳐서 로드)를 들고 있는 안으로 갔었으나, **SOSceneData는 씬 하나당 하나(단일 `AssetReference`)로 단순화**하고, "여러 스테이지 중 어떤 걸 로드할지"의 List는 `GameSceneManager`가 `List<SOSceneData>`로 들고 있는 구조로 정정. 데이터(SO)는 순수하게 하나의 씬 단위 정보만 갖고, "여러 스테이지를 순서대로 관리"하는 건 런타임 매니저의 책임이라는 원칙에 더 맞음.
- `SelectStage`의 이미지 idx ↔ `GameSceneManager.m_listSceneData`의 idx가 1:1 대응.

**3) GameSceneManager — 씬 전환 중에도 살아있어야 진행률을 그릴 수 있음**
- `SelectStage`는 로비 씬, 기존 `GameSceneManager`는 메인 씬 소속이라 서로 다른 씬. 로딩 진행 이미지를 보여주려면 씬이 바뀌는 동안에도 그 컴포넌트가 죽지 않아야 해서, `FeatureManager`와 동일한 패턴(`m_Instance` + `DontDestroyOnLoad`)의 싱글톤으로 전환.
- 기존에 `Start()`에서 처리하던 오브젝트 풀 로딩은, 이제 인스턴스가 씬이 바뀌어도 재생성되지 않으므로 `Start()`가 아니라 `LoadStage → LoadSceneAsync` 흐름 안으로 이동.

**4) 진행률 버그 — "씬 로드 끝나면 100%로 보이는" 문제**
- 처음엔 `Addressables.LoadSceneAsync(...).ToUniTask(IProgress<float>)`의 진행률만 그대로 이미지 `fillAmount`에 꽂았는데, 리뷰 중 "씬만 로드되면 100% 다 채워지는 거 아니냐, 뒤에 풀 로딩도 끝나야지"라는 지적으로 버그 확인. 씬 로드가 끝나는 순간 바로 1.0이 되고, 그 뒤 풀 로딩 동안은 갱신이 전혀 없었음.
- **해결**: 전체 진행률을 씬 로드(0~0.5)와 풀 로드(0.5~1.0) 두 구간으로 나눠서 매핑하도록 수정.

**5) ObjectPool.LoadPoolAsync — 진행률 보고 + 추가 할당 없이**
- 풀 로딩(`LoadPoolAsync`)은 원래 진행률을 전혀 보고하지 않아서, `IProgress<float>` 파라미터를 추가하고 개별 프리팹 로드가 (병렬로) 끝날 때마다 "완료 개수 / 전체 개수"를 보고하도록 함.
- 처음엔 완료 개수 카운터를 `int[] arrCompleteCount = { 0 }`로 박스에 담아 넘겼는데, "이거 매개변수로 그냥 인스턴스 필드로 잡으면 추가 할당 없잖아"라는 지적을 받고 `m_iLoadCount`(인스턴스 필드, `LoadPoolAsync` 진입 시 리셋)로 교체 — 호출마다 배열 힙 할당이 생기던 걸 없앰. CLAUDE.md의 "GC Alloc 0 추구" 원칙과 직결되는 부분이라 기록.

## 3. 🛠️ 반영된 핵심 코드 변경 사항

### 🔹 SOSceneData.cs
```csharp
[CreateAssetMenu(fileName = "SO_SceneData", menuName = "Game/Load/SceneData")]
public class SOSceneData : ScriptableObject
{
    public AssetReference SceneAddress; //Addressable로 등록된 씬 주소 (씬 하나당 SOSceneData 하나)
    public List<SOPoolData> PoolDataList = new List<SOPoolData>();
}
```

### 🔹 GameSceneManager.cs (싱글톤 전환 + Addressable 씬 로드)
```csharp
public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager m_Instance = null;

    [SerializeField] private List<SOSceneData> m_listSceneData = new List<SOSceneData>();
    [SerializeField] private Image m_refProgressImage;

    public int SelectedStageIdx { get; private set; } = 0;

    private void Awake()
    {
        if (m_Instance != null) { Destroy(gameObject); return; }
        m_Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadStage(int _iStageIdx)
    {
        SelectedStageIdx = _iStageIdx;
        LoadSceneAsync(m_listSceneData[_iStageIdx]).Forget();
    }

    private async UniTaskVoid LoadSceneAsync(SOSceneData _refSceneData)
    {
        // 씬 로드 0~0.5, 풀 로드 0.5~1.0
        var refSceneProgress = Progress.Create<float>(fPercent => SetProgress(fPercent * 0.5f));
        var refPoolProgress = Progress.Create<float>(fPercent => SetProgress(0.5f + fPercent * 0.5f));

        var tHandle = Addressables.LoadSceneAsync(_refSceneData.SceneAddress, LoadSceneMode.Single);
        await tHandle.ToUniTask(refSceneProgress, cancellationToken: this.GetCancellationTokenOnDestroy());

        await ObjectPool.m_Instance.LoadPoolAsync(_refSceneData.PoolDataList, this.GetCancellationTokenOnDestroy(), refPoolProgress);
    }
}
```

### 🔹 ObjectPool.cs (진행률 보고, 추가 할당 없음)
```csharp
private int m_iLoadCount = 0;

public async UniTask LoadPoolAsync(List<SOPoolData> _listPoolData, CancellationToken _token = default, IProgress<float> _refProgress = null)
{
    m_iLoadCount = 0;
    ClearPool();
    // ... IntanceAsync(idx, totalCount, _refProgress)를 UniTask.WhenAll로 병렬 실행
}

private async UniTask IntanceAsync(SOPoolData _refData, CancellationToken _token, int _iTotalCount, IProgress<float> _refProgress)
{
    // ... 로드/인스턴스화 ...
    ++m_iLoadCount;
    _refProgress?.Report((float)m_iLoadCount / _iTotalCount);
}
```

### 🔹 SelectStage.cs (신규)
- `RectMask2D` 콘텐츠를 이전/다음 버튼으로 `DOTween` 오프셋 500씩 이동, 이미지 클릭 시 `GameSceneManager.m_Instance.LoadStage(idx)` 호출.

### 🔹 DungeonManager.cs
- `StartStage(0)` 고정값을 `GameSceneManager.m_Instance.SelectedStageIdx`로 교체 — 로비에서 고른 스테이지가 실제 던전 진행에 반영됨.

## 4. 🧭 아직 남은 것 (에디터 작업 / TODO)

- 로비를 제외한 씬들을 Addressable Groups에서 Addressable로 마킹하고, 각 `SOSceneData.SceneAddress`에 연결하는 작업은 에디터 UI에서 직접 해야 함 (스크립트로 대체 불가).
- `GameSceneManager` 오브젝트를 로비 씬으로 옮기고 `m_listSceneData`/`m_refProgressImage`를 인스펙터에서 채워야 함.
- 지난 항목에서 남겨둔 "로비 진입 시점부터 전체 스테이지 몬스터 프리팹이 메모리에 올라오는 문제"(`SOStage.SpawnEntry.MonsterPrefab` 하드 레퍼런스)는 이번 작업 범위에 포함하지 않음 — `Docs/TODO.md` 기록 그대로 유효.

---

# 🚀 [Unity 우주 슈팅] ObjectPool — InstantiateAsync 취소 시 오브젝트 유실 버그 수정

- **날짜:** 2026-07-27
- **관련 시스템:** Object Pool, Async(UniTask), Optimization

## 1. 🚨 문제

- Play 모드에서 `ObjectPool.IntanceAsync`가 `Object.InstantiateAsync`로 대량 인스턴스화를 진행하는 도중 Play를 중단하면, 참조를 잃은 오브젝트가 에디터(Edit 모드) 씬에 그대로 남는 현상 발견.

## 2. 💡 원인 분석

- 프로젝트에 벤더링된 UniTask 소스(`Assets/Plugins/UniTask/Runtime/UnityAsyncExtensions.AsyncInstantiate.cs`)를 직접 확인.
- `AsyncInstantiateOperationConfiguredSource<T>.MoveNext()`/`Continuation()`은 `CancellationToken`이 취소되면 `core.TrySetCanceled()`로 **C# 쪽 `await`만 취소**시킬 뿐, `AsyncInstantiateOperation<T>` 자신의 `Cancel()`은 어디에서도 호출하지 않음.
- 즉 토큰 취소는 "기다림"만 멈출 뿐 Unity 엔진의 인스턴스화 Job(Job System 기반)은 계속 실행됨. `IntanceAsync`는 이미 예외로 빠져나가 `for` 루프(→ `m_hashPool`/`m_hashHandle` 등록)를 타지 않으므로, 뒤늦게 완성된 오브젝트들은 아무도 추적하지 않는 고아가 됨. 완료 시점이 Play→Edit 전환 경계와 겹치면 그대로 에디터에 남는다.

## 3. 🛠️ 반영된 핵심 코드 변경 사항

### 🔹 ObjectPool.cs (`IntanceAsync`)
```csharp
var tOpInstantiate = UnityEngine.Object.InstantiateAsync(refPrefab, _refData.PreLoad);
GameObject[] arrInstance;
using (_token.Register(() => tOpInstantiate.Cancel()))
{
    arrInstance = await tOpInstantiate.ToUniTask(cancellationToken: _token);
}
```
- `_token.Register`로 토큰 취소 시 `AsyncInstantiateOperation<T>.Cancel()`을 직접 호출하도록 연결 — 취소되는 순간 엔진 Job 자체가 멈춰서 고아 오브젝트가 생기지 않음.

## 4. 💡 인사이트

> `CancellationToken`을 async API에 넘기는 것과, "취소됐을 때 그 안의 리소스를 실제로 정리하는 것"은 별개의 책임이다. UniTask 같은 래퍼가 알아서 다 처리해줄 거라 가정하면 안 되고, 특히 자체 `Cancel()` API를 가진 엔진 레벨 오퍼레이션(`AsyncInstantiateOperation` 등)은 `CancellationToken.Register`로 직접 연결해줘야 한다. 벤더링된 라이브러리 소스가 프로젝트에 있으면 "그럴 것이다" 추측 대신 직접 읽고 확인하는 게 빠르고 정확하다.

---

# 🚀 [Unity 우주 슈팅] 아이템 시스템 설계 — SOItemData / ItemManager

- **날짜:** 2026-07-27
- **관련 시스템:** Item, Inventory, ScriptableObject, Event System

## 1. 🚨 목표

- 아이템 데이터 보관 방식과, 획득/장착 시 실제 효과를 플레이어에게 적용하는 흐름을 설계.

## 2. 💡 설계 논의 (대화로 좁혀나간 과정)

**1) Item 런타임 클래스 도입 여부 — 기각**
- 초안: `SOItemData`(`SOData` 상속)를 `Item`이라는 런타임 클래스가 들고 동적으로 데이터를 전달하는 구조.
- 클릭/선택 입력 처리는 이미 `Container`(`BaseButtonUI` 상속)가 담당하고 있어서, `Item`이 실제로 할 일이 없다는 걸 스스로 확인하고 클래스 자체를 생략 — SO만으로 처리.

**2) `SOFeature`식 abstract 메서드 패턴 적용 여부 — 기각**
- 초안: `SOFeature.Apply(Player, int)`처럼 `SOItemData`에도 `abstract void Use(Player)`를 두고 아이템별 서브클래스가 오버라이드.
- 사용자 지적: "데이터 위주 SO를 상속으로 계속 늘리지 말라." `SOFeature`의 abstract-메서드+서브클래스 패턴은 정말 재사용 가능한 "기능" 단위에만 쓰는 것이고, `SOItemData`처럼 순수 데이터 SO는 필드만 갖고 기능은 소비하는 쪽(매니저)에 둔다는 원칙으로 정리. (관련 판단 기준을 메모리에 기록)

**3) 능력치 데이터 표현 — 포지셔널 슬롯(Vector4/int[]) 대신 (enum, 값) 리스트**
- 초안(사용자): `Vector4` 또는 `int[4~8]` 고정 슬롯 + 슬롯 의미를 설명하는 별도 문자열(`ItemDataDesc`).
- 문제 제기: 슬롯 위치가 뭘 의미하는지 코드가 전혀 모르므로 적용부에서 결국 아이템별 분기가 필요해지고, 슬롯 순서가 바뀌면 조용히 깨짐 — 컴파일 타임 안전성이 없음.
- 채택안: `eStatType` enum + `tStatValue{ eStatType Type; float Value; }` 구조체의 `List<tStatValue>`. 적용부는 이 리스트를 순회하며 enum 하나로 `Player`의 기존 스탯 메서드(`AddHP`/`AddAttack`/`AddDefense`/`AddSpeed`/`UpBulletSpeed`)에 위임 — 아이템 종류별 분기 없이 공용 처리 가능. `SOData.Description`을 그대로 쓰므로 별도 설명 문자열도 불필요해짐.

**4) `Inventory`/`PlayerInterface` 분리 → `ItemManager` 하나로 병합**
- 1차안: `JokerCardManager` 전례(두 `Container`를 각각 들고 `OnSelectEvt` 구독)를 따라 `Inventory`(보관)/`PlayerInterface`(장착·적용) 두 클래스로 분리.
- 사용자 지적: 이 흐름은 로비 전용이고 전투 씬에서는 쓰지 않으므로 굳이 클래스를 나눌 이유가 없음 → `ItemManager` 하나로 병합, 인벤토리용/장착용 `Container` 둘 다 필드로 받아 각자 `OnSelectEvt` 구독.
- 이름은 처음 `ItemContainer`로 제안했으나, 이미 `Container`가 UI 슬롯 클래스 이름으로 쓰이고 있어 혼동 우려 → `FeatureManager`/`JokerCardManager`와 통일감 있는 `ItemManager`로 확정.

**5) 싱글턴 지속성 — `DontDestroyOnLoad` 여부**
- 1차안: 로비 씬에서만 쓰는 오브젝트라 싱글턴 중복 체크(`Destroy`)/`DontDestroyOnLoad` 둘 다 불필요하다고 판단.
- 사용자 정정: 스테이지 클리어로 아이템을 얻는 흐름이 나중에 붙으면, 로비로 돌아올 때마다 상태를 다시 로드하고 무슨 아이템을 먹었는지 역추적해야 하는 문제가 생김 → `FeatureManager`/`JokerCardManager`와 동일한 지속 싱글턴 패턴(중복 체크 + `DontDestroyOnLoad`)으로 유지하기로 확정.

## 3. 🛠️ 반영된 핵심 코드 변경 사항

### 🔹 SOItemData.cs (`Assets/11_UI/Item/`)
- `eStatType`(HP/Attack/Defense/Speed/BulletSpeed), `eEquipType`, `tStatValue{Type, Value}` 정의.
- `SOData` 상속, `List<tStatValue> ListValue` + `eEquipType EquipType` 필드만 갖는 순수 데이터 SO (abstract 메서드 없음).

### 🔹 ItemManager.cs (`Assets/05_Manager/`)
```csharp
[SerializeField] private Container m_refInventoryContainer; // 보관
[SerializeField] private Container m_refInterFaceContainer; // 장착/적용

private void Awake()
{
    if (m_Instance != null) Destroy(this);
    m_Instance = this;
    DontDestroyOnLoad(this);
}
```
- `ApplyItemData`에서 `tStatValue.Type`(`eStatType`) 스위치로 `Player`의 기존 스탯 메서드에 위임하는 공용 적용 루프 작성.

## 4. 🧭 아직 남은 것

- `Start()`의 `OnSelectEvt` 구독 연결(보관/장착 각각 어떤 메서드로 받을지)이 아직 주석 처리된 상태 — 확정 필요.
- 기존 `Inventory.cs`/`PlayerInterface.cs`는 `ItemManager`로 대체되어 더 이상 필요 없지만, 파일 삭제는 에이전트가 임의로 하지 않고 사용자가 직접 진행하기로 함.
- `SOItemData`에 수량 제한, 아이템 종류 세분화 등 추가 필드가 더 필요한지는 미정.

