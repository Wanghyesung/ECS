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
