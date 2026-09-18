---
name: unity-workflow
description: "전체 개발 파이프라인(대화형) — 요구사항 명확화 → 계획(Mermaid) → 구현 → 검증. 각 단계에서 사용자 확인. 무인 실행은 $unity-autopilot."
---

# $unity-workflow — 개발 파이프라인 (대화형)

대상: **사용자 요청의 인자**

4단계: **명확화 → 계획 → 실행 → 검증**. 각 단계는 사용자 확인 후 다음으로. 서브에이전트를 띄우지 않고 **이 세션이 직접** 수행한다 (에이전트 콜드 스타트 비용 회피). 예외: 4단계 리뷰는 `unity-reviewer`(독립 컨텍스트)에 맡긴다.

플래그: `--no-test` 통합 테스트 생성 생략. `--no-review` 독립 리뷰 생략.

## 1단계: 명확화

요구사항이 이미 구체적이면 요약만 제시하고 확인받는다. 모호하면 `deep-interview` 채점 기준(Scope/Platform/Performance/Integration/Acceptance 0~2점, 합계 6 미만이면 질문)으로 **라운드당 최대 3개** 질문.

`.codex/docs/prd/`에 같은 기능의 PRD가 있으면 그걸 요구사항으로 쓴다 (`workflow_view` 스킬이 만든 문서).

## 2단계: 계획

1. **탐색** — rg/rg --files(또는 내장 Explore 에이전트)으로 비슷한 기존 코드·필드·메서드를 찾는다. AGENTS.md 규칙대로 ① 비슷한 기존 코드 ② Update/FixedUpdate 성능 영향 ③ SO/이벤트 확장 지점을 한 줄씩 먼저 보여준다.
2. **계획 작성** — 관련 기존 코드를 `파일:필드/메서드`로 구체 인용. 생성/수정 스크립트(경로), 씬 변경(GameObject/컴포넌트 — 프리팹만 보지 말고 필요하면 MCP로 실제 씬 인스턴스 확인), 의존성, 위험(직렬화/성능), **Mermaid 시퀀스 다이어그램 하나**(`rules/architecture.md` 규칙). C# 코드는 `csharp` 코드 펜스로 표시하고 조건식은 최대 3개 조건 규칙을 지킨다.
3. 계획을 제시하고 승인을 기다린다. 승인 전에 코드를 쓰지 않는다.

## 3단계: 실행

1. `.codex/rules/` 전부 준수하며 C# 작성 (`m_` + 헝가리안, UniTask, 풀링, `== null`).
2. 씬 요소는 MCP로 (`batch_execute`로 묶어서). 씬/프리팹/.meta 직접 편집은 훅이 차단한다.
3. **통합 테스트** (`--no-test` 없으면) — 기능이 씬 내 여러 오브젝트/물리/애니메이션 상호작용을 포함하면: 테스트 씬 경로(`Assets/Tests/PlayMode/Scenes/<FeatureName>/`)와 검증할 동작 체크리스트를 제시 → 확인 → MCP로 테스트 씬 구성 → `#if UNITY_INCLUDE_TESTS`로 감싼 `[UnityTest]` 작성. Given–When–Then 문서 작성은 필요 없다. 순수 로직/데이터 변경뿐이면 EditMode 테스트 또는 생략(사유 명시).
4. 주요 단계마다 `refresh_unity(compile: request)` → `read_console(types: error)`로 컴파일 확인. 에러는 진행 전에 고친다.

## 4단계: 검증

1. `refresh_unity` → `read_console` 에러 0 확인.
2. `run_tests` (EditMode → PlayMode, PlayMode는 `init_timeout: 120000`) → `get_test_job(wait_timeout: 60)`으로 결과 수집.
3. `--no-review` 없으면 **`unity-reviewer` 에이전트**에 `git diff --name-only HEAD`의 .cs 목록만 넘겨 독립 리뷰. 심각(Critical) 항목은 고치고 2번 재실행. 성능/제안 항목은 보고만.
4. **Deslop** — 이번에 만든/고친 파일에서만: 구현체 하나뿐인 인터페이스, 코드를 반복하는 주석, 절대 null 아닌 값의 null 체크, 죽은 코드, 불필요한 패턴 제거. 기존 코드는 건드리지 않고, 의심스러우면 둔다. 고친 뒤 `read_console` 재확인.

## 최종 요약

`workflow_view`에서 호출한 경우 검증 완료 후 그 스킬의 6단계(PR 생성·Slack 알림·사용자 리뷰 대기)로 돌아간다. 여기서 PRD를 `done`으로 확정하거나 같은 PR/알림을 중복 생성하지 않는다. 단독 `$unity-workflow` 호출은 기존 동작을 유지한다.

```
## Workflow Complete
### 만든 것 / 수정한 파일
### 검증: 컴파일 · 테스트(통과/실패 수, 통합 테스트 경로) · 리뷰 결과
### 수동 작업 필요: 인스펙터 할당, 씬 참조, 에셋 생성 등
### 테스트 방법
```

## 원칙

- 단계 게이트는 사용자 확인 — 건너뛰지 않는다 (무인은 `$unity-autopilot`)
- 기존 패턴 우선, 최소 구현, 검증은 선택이 아님
