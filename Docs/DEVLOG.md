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


