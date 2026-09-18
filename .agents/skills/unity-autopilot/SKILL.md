---
name: unity-autopilot
description: "무인 실행 — status: approved인 PRD 하나를 질문 없이 구현→컴파일→테스트→독립 리뷰→커밋까지 수행하고 결과를 기록. autopilot.ps1이 codex exec로 호출. 대화형에서는 $unity-workflow를 쓸 것."
---

# $unity-autopilot — 무인 PRD 실행

PRD: **사용자 요청의 인자**

사람이 없다. **절대 질문하지 않는다**(사용자 질문 도구 금지). 판단이 필요하면 PRD의 결정/제약을 따르고, 없으면 가장 보수적인 해석을 택해 결과 섹션에 "가정"으로 기록한다.

## 0. 전제 확인 (하나라도 실패하면 아무것도 건드리지 않고 종료)

- `git branch --show-current` 가 `auto/` 로 시작해야 한다 (autopilot.ps1이 만든 브랜치). 아니면 종료.
- PRD 파일이 존재하고 프런트매터가 `status: approved` 여야 한다. 아니면 종료.
- `refresh_unity` → `read_console(types: error)` 에 **시작 전부터** 컴파일 에러가 있으면 종료 (이전 작업의 잔해를 이 PRD 탓으로 만들지 않는다).

## 1. 요구사항 = PRD

PRD의 Overview / User Flow / Functional Requirements / Technical Architecture / Acceptance Criteria(Given-When-Then)를 읽는다. `.codex/docs/game-design.md` 의 인용 섹션이 있으면 그것도 읽는다. **PRD 범위 밖의 코드는 건드리지 않는다.**

## 2. 탐색 → 구현

1. rg/rg --files으로 비슷한 기존 코드·필드·메서드를 찾아 재사용한다 (새로 만들기 전에 반드시).
2. `.codex/rules/` 전부 준수 (`m_` 헝가리안, UniTask, 풀링, `== null`, SO에 런타임 상태 금지).
3. 씬/프리팹/SO 변경은 MCP(`manage_gameobject`/`manage_components`/`manage_scriptable_object`/`batch_execute`). 파일 직접 편집은 훅이 차단한다.
4. 인스펙터 할당이 필요한 배선은 MCP로 직접 한다. MCP로 불가능한 것(스프라이트 아틀라스, 외부 에셋 등)은 **막힘이 아니라** 결과 섹션의 "수동 작업" 목록에 적고 계속 진행한다.
5. 새 패키지가 필요하거나 `ProjectSettings/`·`Packages/manifest.json` 변경이 필요하면 → **막힘(blocked)** 처리(6단계).

## 3. 컴파일 게이트

`refresh_unity(mode: if_dirty, compile: request, wait_for_ready: true)` → `read_console(types: error, count: 40)`.
`error CSxxxx` 가 있으면 고치고 반복. **5회** 안에 0이 안 되면 막힘 처리.

## 4. 테스트

1. PRD의 Given-When-Then 을 테스트로 옮긴다 — 순수 로직은 `Assets/Tests/Editor/`(EditMode), 씬/물리/생명주기는 `Assets/Tests/PlayMode/`(필요 시 `Scenes/<Feature>/` 테스트 씬을 MCP로 구성). asmdef 없으면 만든다.
2. `run_tests(mode: EditMode)` → `get_test_job(job_id, wait_timeout: 60, include_failed_tests: true)`; PlayMode는 `init_timeout: 120000`.
3. 실패는 고친다 (**3회**). 이번 변경으로 **기존 테스트**가 깨졌으면 반드시 고친다. 3회 안에 안 되면 막힘.

## 5. 독립 리뷰

`unity-reviewer` 에이전트에 **`git diff --name-only HEAD` 의 .cs 목록만** 넘겨 리뷰시킨다 (대화 컨텍스트를 주지 않는다 — 신선한 눈). Critical 항목은 고치고 3→4를 다시 돈다. 성능/제안 항목은 결과 섹션에 적는다.

## 6. 기록 + 커밋

**성공:**
1. PRD 프런트매터 `status: done`, 맨 아래 `## Result (autopilot <날짜>)` 섹션: 만든/수정 파일, 테스트 결과(통과/실패 수, 경로), 리뷰에서 남긴 제안, 가정, 수동 작업 목록.
2. `git add -- <이번 PRD에서 변경한 파일 경로만>` (ProjectSettings/Packages 는 절대 add 하지 않는다 — 훅이 차단)
3. `git commit -m "[Auto] <FeatureName> — PRD: .codex/docs/prd/<X>.md"` (커밋 메시지 끝에 `Co-Authored-By: Codex <noreply@openai.com>`). 훅이 커밋을 막으면(`DISABLE_HOOK_BLOCK_GIT_COMMIT` 미설정) 커밋 없이 결과만 남기고 그 사실을 Result에 적는다.

**막힘(blocked):**
1. PRD 프런트매터 `status: blocked`, `## Blocked (autopilot <날짜>)` 섹션: 어디서(단계), 왜, 마지막 에러/실패 테스트 원문, 사람이 결정해야 할 것.
2. 코드 변경은 `git stash push -u -m "autopilot blocked: <X>" -- <이번 PRD의 코드 변경 경로만>` 로 치운다 — 다음 PRD가 깨끗한 상태에서 시작해야 하므로. 아침에 `git stash list` 로 확인.
3. PRD 파일만 `git add` + 커밋 `[Auto][BLOCKED] <FeatureName>`.

## 7. 마지막 출력 (autopilot.ps1이 로그로 저장)

```
AUTOPILOT RESULT: done | blocked
PRD: <경로>
Commit: <해시 또는 none>
Tests: <통과>/<실패>
Manual: <수동 작업 개수>
```

## 금지

- 질문, 계획 승인 요청, "진행할까요?" — 전부 금지. 결정은 PRD → 규칙 → 보수적 해석 순.
- PRD 범위 밖 리팩터링. `known-issues.md` 편집(사용자 승인이 필요한 문서이므로 무인 모드에서는 변경하지 않음 — PRD Result 에 적는다).
- `git reset --hard` / `git clean` / force push / ProjectSettings 수정 — 훅이 막고, 우회하지 않는다.
