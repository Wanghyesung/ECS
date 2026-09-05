# 전투 진행(EXP/레벨) 시스템 설계 기록

몬스터를 처치했을 때 경험치를 누적하고, Max 도달 시 레벨업 → 기존 랜덤 기능(Feature) 카드 시스템을 호출하는 흐름에 대한 논의 및 결정 사항 기록.

## 요구사항
- 플레이어가 몬스터를 잡으면 경험치 획득
- 경험치가 Max까지 차면 `FeatureManager`(정확히는 카드 UI를 담당하는 `CardCreator`)를 호출해 랜덤 기능 카드 노출
- `FeatureSystem_Design.md`에서 미구현으로 남겨뒀던 "레벨업/경험치 시스템 자체"를 채우는 작업

## 논의 및 결정

### 1. HP와 EXP를 같은 매니저가 들고 있을지
- 처음 논의: BattleManager를 만들면 Player의 HP/EXP를 전부 거기로 옮길지 고민
- **결정**: **HP는 Player/Monster가 각자 소유, EXP/레벨만 BattleManager가 소유**
  - HP는 `IDamageable.TakeDamage`에서 즉시 넉백/카메라 셰이크/HP바 갱신까지 얽힌 **엔티티 자신의 전투 상태**. Monster도 자기 Blackboard에 HP를 들고 있어서 대칭을 맞춤. BattleManager로 옮기면 TakeDamage가 매번 외부 매니저를 참조해야 해서 결합도만 올라감
  - EXP/레벨은 엔티티 상태가 아니라 "이번 판이 얼마나 진행됐는가"를 나타내는 **런(run) 진행 지표**이자 FeatureManager 트리거라는 별도 책임을 가지므로 BattleManager 소유가 자연스러움

### 2. BattleManager의 역할 범위
- 기존 매니저들(`FeatureManager`, `CameraManager`, `TargetManager`)과 동일하게 **싱글턴 + DontDestroyOnLoad + 단일 책임** 패턴을 따름
- Monster 사망 → EXP 누적 → 레벨업 판정까지만 담당하고, 카드를 뽑고 보여주는 로직은 기존 `CardCreator`/`FeatureManager`를 그대로 재사용 (레벨업 시 `CardCreator.ShowChoices()` 호출)
- `OnExpChanged(current, max)`, `OnLevelUp(level)` 이벤트로 열어둬서 UI 쪽과는 Observer 패턴으로 느슨하게 연결 (CLAUDE.md의 Event System 규칙 준수)

### 3. 몬스터 사망 판정이 아예 없었음
- `Monster.TakeDamage`는 HP를 깎기만 하고 사망 처리가 없었음 (넉백 코루틴만 존재)
- **결정**: HP <= 0이면 `Dead()` 호출 → 상태를 `eEntityState.Dead`로 변경, 정적 이벤트 `OnMonsterDied(int _iExpReward)` 발행 후 `SetActive(false)`
  - 정적 이벤트로 만든 이유: BattleManager가 씬의 개별 Monster 인스턴스를 몰라도(스폰되는 몬스터마다 참조를 주고받을 필요 없이) 구독 한 번으로 모든 몬스터의 사망을 알 수 있음
  - 몬스터는 아직 Object Pool 대상이 아니라서(총알/이펙트만 풀링 중) 우선 `SetActive(false)`로 처리, 풀링 도입 시 `ObjectPool.PushObject`로 교체하도록 TODO 남김
  - `TakeDamage`는 `Hit` 또는 `Dead` 상태면 무시하도록 가드 추가 (중복 사망 이벤트 방지)

### 4. EXP 보상 데이터 위치: SOObjectInfo 확장 vs 서브클래스
- 처음엔 `SOObjectInfo`(Player/Monster 공용)에 `ExpReward` 필드를 바로 추가하는 안으로 시작
- **사용자가 `SOMonsterInfo : SOObjectInfo` 서브클래스로 분리**해서 `ExpReward`를 몬스터 전용 데이터로 격리
  - Player의 SO에는 필요 없는 필드가 노출되지 않도록 함 (CLAUDE.md의 "SO는 데이터와 에디터 세팅만" 규칙과 궤를 같이함 — 몬스터 전용 데이터는 몬스터 전용 SO에)
  - `Monster.cs`의 `m_SOMonsterInfo` 필드 타입도 `SOObjectInfo` → `SOMonsterInfo`로 변경됨

### 5. 카드를 고르는 동안 나머지 게임은 멈출지
- 요구사항: 레벨업 카드가 떠 있는 동안은 기능 카드 UI를 제외한 나머지(몬스터 BT, 무기 발사, 넉백, 애니메이션 등)가 전부 멈춰야 함
- **결정**: 각 시스템에 개별 Pause 플래그를 심는 대신 **`Time.timeScale = 0f`** 사용
  - 코드 전반이 이미 `Time.time`/`Time.deltaTime` 기반이라(`Weapon.CheckTime`, `Monster`/`Player`의 넉백·DoT 코루틴, `Animator` 기본 `updateMode = Normal`) timeScale만 0으로 만들면 BT/FSM/무기 쿨타임 어디에도 손대지 않고 자동으로 멈춤
  - 상태 머신마다 Paused 상태를 추가하는 대안은 BT/FSM/무기 타이머를 전부 건드려야 해서 기각
  - `CardCreator.ShowChoices()`에서 `Time.timeScale = 0f`, 카드를 고르면(`HandleCardClicked`) `Time.timeScale = 1f`로 복구 — CardCreator가 이미 카드 UI를 열고 닫는 시점을 전담하고 있어 책임 분리에 맞음
- **주의점**
  - 카드 UI 자체 연출(`RandomFeatureCard.CORotate`의 카드 회전)은 `Time.deltaTime` 대신 **`Time.unscaledDeltaTime`**으로 변경 — timeScale이 0이어도 카드 연출만은 계속 움직여야 하므로
  - `Player.Update()`의 `Input.GetKey` 폴링 자체는 timeScale과 무관하게 계속 호출되지만, `Weapon.CheckTime()`이 `Time.time` 정지로 항상 false를 반환해 실제 발사는 일어나지 않음 (동작상 문제 없음)
  - UI 클릭(EventSystem)은 timeScale과 무관하게 정상 동작하므로 카드 클릭에는 영향 없음

### 6. 몬스터 사망 즉시 카드가 뜨는 위화감
- 문제: 기존엔 `AddExp`에서 Max를 넘는 순간 바로 `LevelUp()` → `CardCreator.ShowChoices()`를 호출해서, EXP 슬라이더가 실제로 꽉 차는 연출을 보여주기도 전에(심지어 슬라이더가 레벨업 후 잔여치로 리셋된 값을 받기 전에) 카드가 떠버림 → "EXP가 다 차지도 않았는데 기능을 고를 수 있다"는 위화감
- **결정**: 레벨업 확정 시점을 **몬스터 사망(EXP 임계값 도달) 시점이 아니라, EXP 슬라이더가 실제로 Max까지 다 차는 애니메이션이 끝나는 시점**으로 미룸
  - `SliderImage`에 이벤트를 하나 더 추가: 기존 `OnFillCompleted`(목표치가 0이 되는 즉시 발생, HP 슬라이더 → `Player.Dead()`에서 사용 중이라 시맨틱을 안 건드림)는 그대로 두고, **`OnFillMaxReached`**를 신설 — `CoLerpSlider` 코루틴이 실제로 끝난 뒤 `_fEndFill >= 1.0f`일 때만 발행
  - `BattleManager.AddExp`는 Max를 넘긴 만큼을 `m_iPendingLevelUps`에 보류만 해두고, `OnExpChanged(m_iMaxExp, m_iMaxExp)`를 발행해 슬라이더를 Max까지 채우는 연출을 재생
  - `Player`가 EX 슬라이더의 `OnFillMaxReached`를 구독해서 `BattleManager.LevelUp()`을 호출 — 이 시점에 실제로 레벨이 오르고 카드 UI가 뜸
  - 카드를 고르는 동안은 `Time.timeScale = 0`(5번 항목)이라 슬라이더가 Max에 멈춰있다가, 카드를 고르고 timeScale이 복구되면 그제야 실제 잔여 경험치로 자연스럽게 줄어듦

### 7. 한 번에 여러 레벨이 오를 때 카드도 순차적으로 띄우기
- 문제: 보스 처치 등으로 한 번에 여러 레벨을 넘길 수 있는데(`while`로 초과분 이월), 6번 결정만으로는 슬라이더가 한 번 Max를 찍는 연출 뒤에 보류된 레벨업이 전부 한꺼번에 처리되어 카드가 제대로 순서대로 노출되지 않음
- **결정**: `BattleManager`에 `CoLevelUP` 코루틴을 두고, `m_iPendingLevelUps`가 남아있는 동안 **레벨 하나당 Max 채우기 연출 → 카드 노출 → 선택 완료를 한 사이클씩** 반복
  - `AddExp`는 초과분만큼 `m_iPendingLevelUps`를 누적하고, 코루틴이 이미 돌고 있지 않을 때만 `StartCoroutine(CoLevelUP())` (진행 중인 루프가 늘어난 pending 값을 그대로 이어받으므로 코루틴 중복 실행 불필요)
  - `CoLevelUP`: `OnExpChanged(Max, Max)` 발행 → `Player`가 슬라이더 완료 시 호출하는 `LevelUp()`이 `m_iPendingLevelUps`를 줄일 때까지 `WaitUntil`로 대기 → pending이 남아있으면 반복, 다 처리되면 실제 잔여 경험치로 슬라이더 복귀
  - 카드 선택 중엔 timeScale이 0이라 다음 사이클의 슬라이더 연출도 자동으로 멈춰있다가, 카드를 고르면 이어서 재생됨 (5, 6번 결정과 자연스럽게 맞물림)

## 최종 구조

```
BattleManager (MonoBehaviour, 싱글턴, DontDestroyOnLoad)
 ├─ int m_iCurrentExp / m_iMaxExp / m_iCurrentLevel
 ├─ int m_iPendingLevelUps                    (ExSlider가 다 찰 때까지 미뤄둔 레벨업 개수)
 ├─ Monster.OnMonsterDied 구독               → AddExp(expReward)
 ├─ AddExp(int)                              → EXP 누적, Max 도달 시 while로 초과분 이월(m_iPendingLevelUps++),
 │                                              CoLevelUP 코루틴 시작(중복 실행 방지)
 ├─ CoLevelUP()                              → pending 1개당: OnExpChanged(Max,Max) 발행 →
 │                                              LevelUp() 호출로 pending 감소할 때까지 WaitUntil 대기 → 반복,
 │                                              끝나면 OnExpChanged(실제 잔여치, Max)로 복귀
 ├─ LevelUp()                                → ExSlider의 OnFillMaxReached 시점에 Player가 호출.
 │                                              레벨 +1, pending-1, OnLevelUp 발행, CardCreator.ShowChoices() 호출
 ├─ event OnExpChanged(int current, int max) → UI(EXP 슬라이더) 구독용
 └─ event OnLevelUp(int level)               → 필요 시 다른 시스템 구독용

Monster (MonoBehaviour)
 ├─ static event OnMonsterDied(int expReward)
 ├─ TakeDamage(...)  → HP <= 0 이면 Dead() 호출, Hit/Dead 상태면 무시
 └─ Dead()           → 상태를 Dead로 변경, OnMonsterDied 발행, SetActive(false)

SOMonsterInfo : SOObjectInfo
 └─ int ExpReward   (몬스터 처치 시 보상 EXP, 몬스터 전용 데이터)

SliderImage (UI 공용 컴포넌트)
 ├─ event OnFillCompleted   → 목표치가 0이 되는 즉시 발생 (HP 슬라이더 → Player.Dead 용, 시맨틱 유지)
 └─ event OnFillMaxReached  → CoLerpSlider 애니메이션이 실제로 Max(1.0)까지 끝난 뒤 발생 (EXP 슬라이더 → 레벨업 확정용)

Player
 ├─ m_refExSliderImage를 BattleManager.OnExpChanged 이벤트 구독으로만 갱신 (표시만 담당)
 └─ m_refExSliderImage.OnFillMaxReached 구독 → BattleManager.LevelUp() 호출

CardCreator
 ├─ ShowChoices()      → 카드 후보 배분 + Time.timeScale = 0f (카드 UI 제외 전부 정지)
 └─ HandleCardClicked  → 기능 적용 + 카드 닫기 + Time.timeScale = 1f (정지 해제)

RandomFeatureCard
 └─ CORotate 카드 회전 연출은 Time.unscaledDeltaTime 사용 (timeScale 0에서도 계속 움직이도록)
```

### 관련 파일
- `Assets\05_Manager\BattleManager.cs` — 신규, EXP/레벨/순차 레벨업(CoLevelUP) 처리
- `Assets\03_Monster\MonsterInfo\Monster.cs` — 사망 판정/이벤트 추가 (`Dead()`, `OnMonsterDied`)
- `Assets\03_Monster\MonsterInfo\SOMonsterInfo.cs` — `SOObjectInfo` 상속 서브클래스로 분리, `ExpReward` 추가
- `Assets\02_Player\Player.cs` — `BattleManager.OnExpChanged`/`SliderImage.OnFillMaxReached` 구독·해제, EXP 슬라이더 갱신 및 레벨업 확정 트리거
- `Assets\11_UI\Script\SliderImage.cs` — `OnFillMaxReached` 이벤트 추가 (애니메이션 완료 시점 발행)
- `Assets\11_UI\Script\CardCreator.cs` — 카드 노출/닫기 시점에 `Time.timeScale` 0/1 토글
- `Assets\11_UI\Script\RandomFeatureCard.cs` — 카드 회전 연출 `Time.unscaledDeltaTime`으로 변경

## 아직 미구현 / 다음에 논의할 부분
- 씬에 `BattleManager` GameObject 배치 및 인스펙터에서 `m_iMaxExp`, `m_refCardCreator` 연결 (에디터 수동 작업)
- 몬스터 SO 에셋별 `ExpReward` 값 세팅
- 몬스터 Object Pool 미도입 상태라 사망 시 `SetActive(false)`만 처리 중 (`Dead()`에 TODO로 표시)
- 플레이어 사망 연출: 자기 축(로컬 X) 기준으로 뒤로 넘어가듯 쓰러지는 모션을 `VisualObject`에 추가하는 안 논의 중, 아직 코드 미적용
- 카드를 고르는 동안 사운드(BGM/효과음)까지 멈출지는 미정 (`AudioListener.pause` 등, 현재는 손대지 않음)
