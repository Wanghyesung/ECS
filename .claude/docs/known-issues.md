# 알려진 문제 (Known Issues)

> Claude Code가 세션 중 발견했지만 아직 고치지 않은 문제를 기록하는 곳. 새 항목은 위에 추가하고, 고치면 해당 항목에 `[해결됨]`을 표시하고 커밋/PR을 남긴 뒤 지우지 말고 남겨둘 것 (재발 방지 기록).

---

## [미해결] 매니저 싱글톤이 MainScene/LobyScene 경로에서 null이 되는 문제 (2026-08-13 진단)

**증상:** `BattleScene.unity`를 직접 열고 Play하면 문제없이 동작하지만, 실제 게임 흐름(`LobyScene` → `GameSceneManager.LoadStage()` → Addressables로 `MainScene` 로드)으로 들어가면 콘솔에 다음이 뜬다:
- `DontDestroyOnLoad only works for root GameObjects or components on root GameObjects.` — `BattleManager`, `InputManager`, `ObjectPool`(PoolManager), `CameraManager`, `FeatureManager` 등에서 발생
- `NullReferenceException` — `Missiles.cs:25`(`MissileMoveManager.m_Instance`), `CircleCollider.cs:68`, `TargetScanner.cs`(`ColliderManager.m_Instance`)에서 발생

**근본 원인은 두 가지이며, 로딩 경로와 무관하게 `MainScene.unity` 자체의 씬 구성 문제다:**

1. **DontDestroyOnLoad 실패**: `MainScene.unity` 안에서 `PoolManager`/`InputManager`/`CameraManager`/`MonsterHPManager`/`FeatManager`/`BattleManager`/`ContainerManager` 7개가 전부 `DonDestoryObjects`(오타 있음)라는 부모 오브젝트의 **자식**으로 배치되어 있다. `DontDestroyOnLoad()`는 씬 **루트** 오브젝트에서만 동작하므로, 이 매니저들의 `Awake()`가 실행되는 순간 항상 이 에러가 난다.
2. **ColliderManager / MissileMoveManager가 아예 배치 안 됨**: 두 스크립트는 `BattleScene.unity`와 `TestScene.unity`에만 존재하고, `MainScene.unity`·`LobyScene.unity` 어디에도 없다. `Player`의 `Missiles`/`CircleCollider`/`TargetScanner`가 이 두 매니저를 참조하는 순간 `m_Instance`가 null이라 터진다.

**해결 방법 (아직 미적용):** 프로젝트 아키텍처 규칙(`.claude/rules/unity-specifics.md`의 "부트스트랩 씬" 패턴)대로, 이 매니저 9개(7개 + ColliderManager + MissileMoveManager)를 전부 `LobyScene`(부트스트랩 씬)으로 옮기고, `DonDestoryObjects` 같은 부모 없이 씬 루트로 배치할 것. 그러면 Loby에서 한 번만 `DontDestroyOnLoad`되고 이후 `MainScene`이 Single 모드로 로드돼도 그대로 살아남아 두 문제가 동시에 해결된다.

> 2026-08-13 후속: 실제 게임 흐름의 로드 대상을 `MainScene` → `BattleScene`으로 변경(`LobyScene`의 `GameSceneManager.m_listSceneData[0]`을 `SO_BattleSceneData`로 교체, `SO_BattleSceneData.asset` 신설). `BattleScene`엔 `ColliderManager`/`MissileMoveManager`가 이미 배치돼 있어서 위 2번 문제는 우회됐지만, 1번(`DonDestoryObjects` 자식 문제)은 `BattleScene`에도 동일하게 있다 — 지금은 유일하게 로드되는 씬이라 당장은 괜찮지만 씬이 하나 더 늘면 재발한다. 근본 해결책은 여전히 위와 동일(매니저를 LobyScene 루트로 이전).

---

## [미해결] `Player`가 프리팹과 연결이 끊긴 채 씬마다 독립 사본으로 존재 (2026-08-13 진단)

**증상:** 미사일 `m_bLookTarget` 버그를 고쳤는데, `MainScene.unity` / `TestScene.unity` / `BattleScene.unity` 세 군데를 전부 따로 고쳐야 했다. 한 곳만 고치면 나머지 씬에는 반영되지 않는다.

**근본 원인:** `Assets/3D/02_Player/Prefab/Player.prefab`이라는 프리팹 에셋이 존재하긴 하지만, 세 씬의 `Player`는 전부 이 프리팹과 **연결이 끊긴 독립 사본**이다 (`Weapon` 컴포넌트의 `m_CorrespondingSourceObject`/`m_PrefabInstance`가 모두 `{fileID: 0}`, 즉 순수 씬 내장 오브젝트). 게다가 `Player.prefab` 자체도 방치된 옛날 버전이라 문제의 원인조차 될 수 없다 — Weapon이 2개뿐이고 전부 `m_eWeaponType: 1`(Bullet)이며, 미사일 웨폰 자체가 없고 `m_bLookTarget`/`m_refFireTr`/`m_iBulletCount` 등 현재 `Weapon.cs`에 있는 필드가 아예 없다(지금 스크립트보다 훨씬 옛날 버전 기준으로 저장된 채 그대로 방치됨).

즉 세 씬이 각자 독립적으로 `Player`를 들고 있고, 공통 프리팹은 이미 오래전에 갈라져서 그 이후로 아무도 안 쓰는 상태다. 그래서 `Player` 관련 버그는 항상 씬 개수만큼 따로 고쳐야 하고, 앞으로 씬을 새로 만들 때(특히 이 오래된 프리팹이나 기존 씬을 복사해서 만들 경우) 과거에 고친 버그가 다시 섞여 들어올 위험이 있다.

**해결 방법 (아직 미적용):** 세 씬 중 가장 최신 상태인 `Player`를 기준으로 `Player.prefab`을 다시 만들고(Apply/Overwrite), 세 씬의 `Player`를 그 프리팹의 실제 인스턴스로 교체할 것. 씬마다 위치/개별 참조가 걸려 있어 잘못 하면 데이터가 깨질 수 있으므로, 손대기 전에 각 씬의 `Player` 하위 설정(웨폰 SO 참조, 이펙트 연결 등)을 먼저 비교해서 차이가 있는지 확인 필요.
