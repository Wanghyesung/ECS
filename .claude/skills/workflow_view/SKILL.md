---
name: workflow_view
description: "채팅에서 슬래시 커맨드 없이 '~구현해줘'/'~만들어줘'/'~생각중이야'처럼 새 기능 구현 의도를 밝히면 자동 발동. .claude/docs/prd/ 기존 문서 확인 → 부족한 요구사항 질문(deep-interview 채점 기준 재사용) → Plan Mode에서 BDD 스타일 PRD+Mermaid 다이어그램 작성/저장 → 복잡도별 에이전트 라우팅 실행(unity-workflow 재사용) → 검증까지 전체 파이프라인을 오케스트레이션."
---

# workflow_view — 대화형 기획→구현 파이프라인

슬래시 커맨드 없이 채팅으로 구현 의도를 말해도 기획(PRD)→계획→구현→검증까지 자동으로 이어지게 한다. **새 로직을 최소화하고 기존 스킬/커맨드를 그대로 오케스트레이션**하는 것이 이 스킬의 유일한 존재 이유다 — 아래 각 단계는 대부분 기존 파일을 인용한다.

## 발동 조건

- 사용자가 구현 의도를 표현: "~해줘", "~만들어줘", "~추가해줘", "~생각중이야", "~하려고 하는데" 등
- 제외(발동 안 함): 버그 수정 요청("에러"/"버그"/"고장"/"크래시" 포함), `--skip-plan` 또는 "그냥 해줘"/"묻지 말고" 명시적 opt-out, 이미 이번 대화에서 요구사항이 확정된 후속 요청, "색만 바꿔줘"류 단순 수정
- 위 기준은 [deep-interview/SKILL.md](.claude/skills/deep-interview/SKILL.md)의 "When to Activate / Exemptions"와 동일한 기준을 공유한다 — 중복 정의하지 않고 그대로 따른다.

## 0단계: 기존 PRD 확인 (신규)

1. 요청 내용에서 기능명 후보를 PascalCase로 추출한다 (예: "조준선 예고 기능" → `AimTelegraph`).
2. `.claude/docs/prd/*.md`를 Glob으로 스캔한다.
3. 이름이 유사하거나 내용이 겹치는 기존 PRD가 있으면 Read로 파싱해서 제시한다: "기존에 `<FeatureName>` PRD가 있습니다 — 이어서 확장할까요, 새로 작성할까요?"
   - **확장**을 고르면 기존 PRD를 베이스로 2단계로 바로 진입(이미 채워진 섹션은 재질문하지 않음)
   - **새로 작성**을 고르면 1단계부터 진행
4. 일치하는 문서가 없으면 그냥 1단계로 진행한다.

## 1단계: 요구사항 게이트 (deep-interview 재사용)

[deep-interview/SKILL.md](.claude/skills/deep-interview/SKILL.md)의 모호성 채점 기준을 그대로 적용한다: Scope/Platform/Performance/Integration/Acceptance Criteria 5개 항목 0~2점, 합계 6/10 미만이면 가장 약한 항목 위주로 **라운드당 최대 3개 질문**.

동시에 CLAUDE.md의 채팅 요청 규칙에 따라 다음 3가지를 1줄씩 조사해 다음 단계 PRD 재료로 쓴다:
1. 비슷한 기존 코드 검색 결과 (Grep/Glob 또는 내장 Explore 에이전트)
2. Update/FixedUpdate 등 핫 루프 성능 영향
3. SO/이벤트로 확장 가능한 지점

## 2단계: Plan Mode 전환 + PRD 작성

1. `EnterPlanMode`로 전환한다.
2. PRD 골격은 [PRD 템플릿](https://gist.github.com/gkossakowski/21cd41fc3801de9d7d0201e0792c7ded) 구조를 따른다: Overview(Purpose/Scope/Tech Stack) → User Flow(Entry/Core Steps/Edge Cases) → Functional Requirements(UI/Core Features/Data&Integration/NFR) → Technical Architecture(System Overview/Component Details/Algorithms/Assets) → Assumptions&Constraints → Implementation Plan(Milestones/Ownership/DoD) → Detailed Specs → Roadmap → External Resources → Changelog.
3. **각 주요 요구사항 항목 뒤에는 반드시 Rationale(왜 이렇게 결정했는지) 한 줄을 붙인다** — 템플릿의 핵심 원칙.
4. **Acceptance Criteria는 BDD 형식(Given-When-Then)으로 작성한다**:
   ```
   Given [사전 상태]
   When [행위/트리거]
   Then [기대 결과]
   ```
   이 목록은 그대로 4단계(테스트 생성)의 어서션 계획으로 재사용한다 — [unity-workflow.md](.claude/commands/unity-workflow.md) 3단계 참고.
5. Technical Architecture 섹션은 Structurizr의 C4 계층(System Context → Container → Component → Dynamic)을 구성 기준으로만 빌려오고, 실제 렌더링은 프로젝트 표준인 **Mermaid**로 한다(외부 툴 업로드 없이 [.claude/rules/architecture.md](.claude/rules/architecture.md)의 "Mermaid 다이어그램 작성 규칙"과 일치시키기 위함):
   - 시스템/컴포넌트 개요 → `flowchart`
   - 상태 전이가 있는 기능 → `stateDiagram-v2`
   - 시간 순서 상호작용(요청↔응답) → `sequenceDiagram`
6. **완성된 PRD 전체(다이어그램 포함)를 Claude Artifact로 publish해서 실제 렌더링된 화면으로 보여준다** — 터미널 텍스트만으로 끝내지 않는다. 이게 이 스킬에서 가장 중요한 산출물이다.
7. 사용자 승인을 받는다. 승인되면 다음을 저장한다:
   - `.claude/docs/prd/<FeatureName>.md` — 맨 위에 YAML 프런트매터 `status: approved` + `artifact: <URL>`, 그 아래 PRD 전문(Mermaid 소스 포함)
   - 승인 전에는 저장하지 않는다(미승인 초안이 문서로 굳는 것 방지)
   - **이 `status` 필드가 밤 무인 실행의 큐다**: `autopilot.ps1`은 `status: approved`인 PRD만 골라 `/unity-autopilot`으로 실행하고, 끝나면 `done`/`blocked`로 바꾼다. 낮에 이 세션이 바로 구현하기로 했으면 저장 직후 `status: in-progress`로 두고 끝나면 `done`으로 바꾼다.
8. 사용자에게 묻는다: **"지금 구현할까요, 아니면 밤 autopilot 큐에 둘까요?"** — 큐에 두면 여기서 끝난다(3~5단계 생략).

## 3단계: 계획 (unity-workflow 재사용)

[unity-workflow.md](.claude/commands/unity-workflow.md) 2단계 — 탐색 결과를 인용한 구현 계획 + Mermaid. PRD의 Technical Architecture가 이미 그 역할이면 중복 작성하지 않고 승인만 받는다.

## 4단계: 실행 (unity-workflow 재사용)

[unity-workflow.md](.claude/commands/unity-workflow.md) 3단계를 이 세션이 직접 수행한다. 통합 테스트는 2단계의 Given-When-Then을 어서션 계획으로 그대로 쓴다.

## 5단계: 검증 (unity-workflow 재사용)

[unity-workflow.md](.claude/commands/unity-workflow.md) 4단계(컴파일 → 테스트 → `unity-reviewer` 독립 리뷰 → Deslop). 끝나면 PRD `status: done` + `## Result` 섹션 기록.

## 방법론 채택 노트

- **BDD(Given-When-Then)**: 채택 — 위 2/4단계에 반영.
- **MSA**: 미채택 — 단일 Unity 실행파일이라 서비스 분리가 성립하지 않음. `.claude/rules/architecture.md`의 "갓 오브젝트 금지"(System별 책임 분리)가 이미 같은 역할을 함.
- **OOP/FP**: 기본은 기존과 동일하게 OOP 유지(MonoBehaviour 컴포넌트 모델이 강제). 데미지 공식/커브 평가 등 순수 계산 로직에 한해 부작용 없는 함수로 작성하는 정도만 권장 — 전역 패러다임 전환 아님.
- **Agile**: 별도 스프린트/보드 없이 이 파이프라인의 0~5단계 자체가 짧은 반복 주기(계획→구현→검증) 역할을 함.

## 설계 원칙

- 각 단계 게이트는 사용자 확인 필수 — 절대 건너뛰지 않는다.
- 기존 `unity-workflow`/`deep-interview`의 로직을 재사용하고 중복 구현하지 않는다. 이 스킬의 차별점은 오직: **자연어 자동 발동 + PRD/다이어그램의 문서화·재사용(0단계, 2단계 저장)**.
- 시각 자료(Mermaid + Artifact)는 선택이 아니라 필수 산출물.
