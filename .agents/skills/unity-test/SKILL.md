---
name: unity-test
description: "누락된 테스트를 작성하고 MCP run_tests로 실행합니다. EditMode/PlayMode 구분, 테스트 씬 구성, 결과 보고."
---

# $unity-test — 테스트 작성 및 실행

범위: **사용자 요청의 인자** (없으면 최근 변경 `.cs` 중 테스트 없는 게임플레이 로직 우선)

이 세션이 직접 수행한다.

## 1. 커버리지 파악
- 기존 테스트: `Assets/Tests/Editor/`(EditMode). 테스트 asmdef가 없으면 `Assets/Tests/Editor/ECS.Tests.Editor.asmdef`, `Assets/Tests/PlayMode/ECS.Tests.PlayMode.asmdef` 생성 (`UNITY_INCLUDE_TESTS` define constraint, nunit/UnityEngine.TestRunner 참조)
- 우선순위: 게임 상태 로직(체력/데미지/점수/카드) > 시스템 > 유틸

## 2. 작성
- MonoBehaviour 생명주기 불필요한 순수 로직 → **EditMode**
- MonoBehaviour/물리/씬 상태 → **PlayMode**. 여러 오브젝트 상호작용이면 `Assets/Tests/PlayMode/Scenes/<Feature>/`에 MCP로 테스트 씬 구성 후 `[UnityTest]`에서 로드
- 이름 `Method_Condition_Expected`, Arrange-Act-Assert, TearDown에서 GameObject 정리
- PRD가 있으면 Given-When-Then을 어서션으로 그대로 옮긴다

## 3. 실행
```
run_tests(mode: EditMode)  → get_test_job(job_id, wait_timeout: 60, include_failed_tests: true)
run_tests(mode: PlayMode, init_timeout: 120000) → get_test_job(...)
```

## 4. 보고
통과/실패/스킵 수, 실패 항목(이름·예상 vs 실제·스택·제안 수정), 새 테스트 파일 경로, 남은 커버리지 공백.
