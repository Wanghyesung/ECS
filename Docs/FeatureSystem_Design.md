# 랜덤 기능(Feature) 시스템 설계 기록

레벨업 등 특정 상황에서 플레이어에게 랜덤 강화/기능을 제시하는 시스템에 대한 논의 및 결정 사항 기록.

## 요구사항
- 미사일 해금, 새 무기 해금, 공격속도 증가 등 다양한 "기능"을 랜덤으로 제시
- 발동 조건: 플레이어 레벨업 시 (다른 상황에서도 발동 가능하도록 열어둠) — 매니저에게 알리는 방식
- 기능 종류
  - **Repeatable**: 이미 얻은 기능이라도 다시 후보로 나올 수 있음 (예: 공격속도 증가)
  - **OneTime**: 한 번 얻으면 다시는 후보로 나오지 않음 (예: 무기 해금)
- 연출(카드 회전, 룰렛 등)은 이번 스코프에서는 미구현, 추후 UI 단에서 `FeatureManager`가 제공하는 후보 리스트를 받아서 연출

## 논의 및 결정

### 1. 상태 저장 방식: Dictionary vs List(enum 인덱스)
- 처음엔 `Dictionary<FeatureSO, int>` 로 획득 상태를 관리하는 안을 제시
- **사용자 피드백**: 기능 목록은 런타임에 추가/삭제되지 않고 고정이라, Dictionary의 해시 계산 + 버킷 탐색 비용을 낼 이유가 없음. `eWeaponType`, `eNodeState`처럼 enum을 인덱스로 쓰는 기존 프로젝트 컨벤션과도 맞지 않음
- **결정**: `eFeatureID` enum + `int[]` 배열로 변경. 배열 인덱스 = `(int)eFeatureID`. GC Alloc 0 / 캐시 지역성 측면에서도 유리

### 2. SO 데이터 오염 방지
- `FeatureSO`는 이름/설명/아이콘/`eAcquireType`/가중치 등 **정적 데이터 + Apply 로직만** 보유
- 런타임 획득 레벨/횟수는 SO가 아니라 `FeatureManager`의 `int[] m_arrAcquiredLevel`이 전담 (CLAUDE.md의 "SO는 데이터와 에디터 세팅만" 규칙 준수)
- BT의 `SONode.Execute(BlackBoard)` 패턴과 동일한 사상: SO는 공유 애셋이라 인스턴스 상태를 못 가짐

### 3. 무기 해금 방식: Instantiate vs 비활성화
- 처음엔 `SOFeatureUnlockMissile`이 무기 프리팹을 들고 있다가 `Instantiate()` 하는 안을 제시
- **사용자 피드백**: 런타임 동적 Instantiate 대신, 무기를 씬/프리팹에 미리 배치해두고 처음엔 비활성화 상태로 두는 게 더 낫다고 판단 (CLAUDE.md의 "매 프레임 `new` 연산자 금지", Object Pool 선호 기조와 일치 — 여기선 Object Pool 대상은 아니지만 동일한 "동적 생성 최소화" 철학)
- **결정**: `Player.m_listWeapon`에 처음부터 모든 무기 슬롯(잠긴 것 포함)을 등록해두고, 잠긴 무기는 `SetActive(false)`. `UnlockWeapon(eWeaponType)`은 `SetActive(true)`만 호출. `Player.Fire()` 루프는 비활성 무기를 스킵하도록 체크 추가
- SO는 씬 오브젝트를 직접 참조할 수 없으므로(프로젝트 애셋이기 때문), 무기 매칭은 `eWeaponType` enum 값으로 함

## 최종 구조

```
FeatureSO (abstract ScriptableObject)
 ├─ SOFeatureAttackSpeedUp   : Repeatable, Weapon.SetCooldownMultiplier() 호출
 └─ SOFeatureUnlockMissile   : OneTime,   Player.UnlockWeapon() 호출

FeatureManager (MonoBehaviour, 싱글턴)
 ├─ FeatureSO[] m_arrFeatureByID     (eFeatureID 인덱스, Awake에서 매핑+검증)
 ├─ int[] m_arrAcquiredLevel         (런타임 획득 상태 전담)
 ├─ RequestFeatureChoices(int _iCount)  → OneTime 이미획득 제외 + 가중치 랜덤 N개 (N은 가변)
 └─ SelectFeature(FeatureSO, Player)    → Apply() 호출 + 상태 갱신 + OnFeatureAcquired 이벤트
```

### 관련 파일
- `Assets\10_Option\Feature\FeatureSO.cs` — `eFeatureID`/`eAcquireType` enum + 베이스 클래스
- `Assets\10_Option\Feature\SOFeatureAttackSpeedUp.cs`
- `Assets\10_Option\Feature\SOFeatureUnlockMissile.cs`
- `Assets\05_Manager\FeatureManager.cs`
- `Assets\02_Player\Player.cs` — `UnlockWeapon()`, `ModifyWeaponCooldown()` 추가, `Fire()` 루프에 비활성 무기 스킵 추가
- `Assets\02_Player\Weapon\Weapon.cs` — `m_fBaseCooldown`, `SetCooldownMultiplier()` 추가 (반복 적용해도 드리프트 없도록 매번 base 기준 재계산)

## 아직 미구현 / 다음에 논의할 부분
- 레벨업/경험치 시스템 자체 (현재 `Player.cs`에 관련 로직 없음). 준비되면 `FeatureManager.m_Instance.RequestFeatureChoices(_iCount)` 호출하는 지점 필요
- 카드 회전, 룰렛 등 실제 연출 UI
- 무기 슬롯을 씬/프리팹에 어떻게 배치할지 (Player 하위 자식으로 전부 미리 두고 잠긴 건 비활성화)
