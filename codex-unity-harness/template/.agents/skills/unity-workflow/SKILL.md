---
name: unity-workflow
description: "명시적으로 호출한 Unity 기능을 조사하고 한 번의 계획 승인 후 구현·검증한다. 승인된 PRD의 무인 실행은 $unity-autopilot을 사용한다."
---

# $unity-workflow — 개발 파이프라인 (대화형)

대상: **사용자 요청의 인자**

4단계: **명확화 → 계획 승인 1회 → 실행 → 검증**. 이 세션이 직접 수행한다. 독립 리뷰는 고위험 변경이나 명시적 요청에만 사용한다.

플래그: `--no-test` 통합 테스트 생성 생략. `--review` 독립 리뷰 강제. `--no-review` 독립 리뷰 금지.

## 1단계: 명확화

요구사항이 이미 구체적이면 요약만 제시하고 확인받는다. 모호하면 `deep-interview` 채점 기준(Scope/Platform/Performance/Integration/Acceptance 0~2점, 합계 6 미만이면 질문)으로 **라운드당 최대 3개** 질문.

`.codex/docs/prd/`에 같은 기능의 PRD가 있으면 그걸 요구사항으로 쓴다.

## 2단계: 계획

1. **탐색** — rg/rg --files(또는 내장 Explore 에이전트)으로 비슷한 기존 코드·필드·메서드를 찾는다. AGENTS.md 규칙대로 ① 비슷한 기존 코드 ② Update/FixedUpdate 성능 영향 ③ SO/이벤트 확장 지점을 한 줄씩 먼저 보여준다.
2. **계획 작성** — 관련 기존 코드를 `파일:필드/메서드`로 구체 인용. 생성/수정 경로, 씬 변경, 의존성, 직렬화·성능 위험을 적는다. 세 개 이상 컴포넌트의 호출 순서를 이해하는 데 도움이 될 때만 Mermaid 시퀀스 다이어그램 하나를 사용한다.
3. 계획을 제시하고 승인을 기다린다. 승인 전에 코드를 쓰지 않는다.

## 3단계: 실행

1. AGENTS.md 라우팅에 따라 작업과 관련된 `.codex/rules/`만 읽고 C#을 작성한다.
2. 씬 요소는 MCP로 (`batch_execute`로 묶어서). 씬/프리팹/.meta 직접 편집은 훅이 차단한다.
3. **통합 테스트** (`--no-test` 없으면) — 기능이 씬 내 여러 오브젝트/물리/애니메이션 상호작용을 포함하면: 테스트 씬 경로(`Assets/Tests/PlayMode/Scenes/<FeatureName>/`)와 검증할 동작 체크리스트를 제시 → 확인 → MCP로 테스트 씬 구성 → `#if UNITY_INCLUDE_TESTS`로 감싼 `[UnityTest]` 작성. Given–When–Then 문서 작성은 필요 없다. 순수 로직/데이터 변경뿐이면 EditMode 테스트 또는 생략(사유 명시).
4. 주요 단계마다 `refresh_unity(compile: request)` → `read_console(types: error)`로 컴파일 확인. 에러는 진행 전에 고친다.

## 4단계: 검증

1. `refresh_unity` → `read_console` 에러 0 확인.
2. `run_tests` (EditMode → PlayMode, PlayMode는 `init_timeout: 120000`) → `get_test_job(wait_timeout: 60)`으로 결과 수집.
3. `--review`가 있거나 직렬화·씬 구조·다중 시스템·핫 루프를 바꾼 고위험 작업일 때만 **`unity-reviewer` 에이전트**에 이번 작업의 .cs 목록을 넘긴다. `--no-review`가 있으면 실행하지 않는다. 심각 항목은 고치고 최대 2번 재검증한다.
4. **Deslop** — 이번에 만든/고친 파일에서만: 구현체 하나뿐인 인터페이스, 코드를 반복하는 주석, 절대 null 아닌 값의 null 체크, 죽은 코드, 불필요한 패턴 제거. 기존 코드는 건드리지 않고, 의심스러우면 둔다. 고친 뒤 `read_console` 재확인.

## 최종 요약

검증 결과를 정리하고 PRD가 있다면 그 상태를 사용자와 합의한 워크플로에 따라 갱신한다.

```
## Workflow Complete
### 만든 것 / 수정한 파일
### 검증: 컴파일 · 테스트(통과/실패 수, 통합 테스트 경로) · 리뷰 결과
### 수동 작업 필요: 인스펙터 할당, 씬 참조, 에셋 생성 등
### 테스트 방법
```

## 원칙

- 사용자 확인은 구현 전 계획 승인 한 번만 받는다. 요구가 이미 승인됐으면 반복하지 않는다.
- 기존 패턴 우선, 최소 구현, 검증은 선택이 아님
