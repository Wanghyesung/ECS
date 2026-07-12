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

## 최종 구조

```
BattleManager (MonoBehaviour, 싱글턴, DontDestroyOnLoad)
 ├─ int m_iCurrentExp / m_iMaxExp / m_iCurrentLevel  (런타임 진행 상태 전담)
 ├─ Monster.OnMonsterDied 구독               → AddExp(expReward)
 ├─ AddExp(int)                              → EXP 누적, Max 도달 시 while로 레벨업 반복(초과분 이월)
 ├─ LevelUp()                                → 레벨 +1, OnLevelUp 발행, CardCreator.ShowChoices() 호출
 ├─ event OnExpChanged(int current, int max) → UI(EXP 슬라이더) 구독용
 └─ event OnLevelUp(int level)               → 필요 시 다른 시스템 구독용

Monster (MonoBehaviour)
 ├─ static event OnMonsterDied(int expReward)
 ├─ TakeDamage(...)  → HP <= 0 이면 Dead() 호출, Hit/Dead 상태면 무시
 └─ Dead()           → 상태를 Dead로 변경, OnMonsterDied 발행, SetActive(false)

SOMonsterInfo : SOObjectInfo
 └─ int ExpReward   (몬스터 처치 시 보상 EXP, 몬스터 전용 데이터)

Player
 └─ m_refExSliderImage를 BattleManager.OnExpChanged 이벤트 구독으로만 갱신
    (HP처럼 Player가 값을 직접 들고 있지 않고, 표시만 담당)
```

### 관련 파일
- `Assets\05_Manager\BattleManager.cs` — 신규
- `Assets\03_Monster\MonsterInfo\Monster.cs` — 사망 판정/이벤트 추가 (`Dead()`, `OnMonsterDied`)
- `Assets\03_Monster\MonsterInfo\SOMonsterInfo.cs` — `SOObjectInfo` 상속 서브클래스로 분리, `ExpReward` 추가
- `Assets\02_Player\Player.cs` — `Start`/`OnDestroy`에서 `BattleManager.OnExpChanged` 구독/해제, `HandleExpChanged`로 EXP 슬라이더 갱신

## 아직 미구현 / 다음에 논의할 부분
- 씬에 `BattleManager` GameObject 배치 및 인스펙터에서 `m_iMaxExp`, `m_refCardCreator` 연결 (에디터 수동 작업)
- 몬스터 SO 에셋별 `ExpReward` 값 세팅
- 몬스터 Object Pool 미도입 상태라 사망 시 `SetActive(false)`만 처리 중 (`Dead()`에 TODO로 표시)
- 플레이어 사망 연출: 자기 축(로컬 X) 기준으로 뒤로 넘어가듯 쓰러지는 모션을 `VisualObject`에 추가하는 안 논의 중, 아직 코드 미적용
