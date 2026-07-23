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
