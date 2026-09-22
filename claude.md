# CLAUDE.md - AI 어시스턴트를 위한 프로젝트 컨텍스트

> 이 파일은 가볍게 유지합니다 — 세부 내용은 `.claude/docs/`와 `.claude/rules/`를 참고하세요.

---

## 프로젝트 개요

| 속성 | 값 |
|------|-----|
| **Unity 버전** | 2022.3.62f2 · URP · 주 타겟 Windows, 부 타겟 Android |
| **패키지** | Addressables, AI Navigation, Cinemachine, Input System, TextMeshPro, Test Framework, Unity MCP |
| **비동기/반응형/트윈** | **UniTask**(`Assets/Plugins/UniTask`, 코루틴 전면 대체 — 필수) · **DOTween**(`Assets/Plugins/Demigiant`) · **R3**(Cysharp — 프로젝트 표준 Reactive. **아직 미설치**: 사용 전 `Packages/manifest.json`에 `com.cysharp.r3` 있는지 확인, 없으면 `r3` 스킬의 설치 안내를 사용자에게 제시하고 코드를 쓰지 말 것) |
| **안 쓰는 것** | VContainer(DI), UniRx(→R3), 코루틴, 레거시 Input, MessagePipe |

**게임 기획:** 3D 슈팅 · 로그라이트. 컨셉/게임 루프/조커 카드 시스템은 `.claude/docs/game-design.md`, 밸런스는 `.claude/docs/balance-guide.md`.

---

## Claude Code 작업 규칙

- 코드를 수정하거나 리팩토링할 때 기존 구조를 최대한 깨뜨리지 않고 유지할 것. 성능적으로 더 좋은 방법이 있다면 기존 구조를 깨뜨려도 됨.
- 새 기능을 추가/수정할 때, 그렇게 설계한 이유를 명확히 설명할 것.
- 매 프레임 Alloc 등 성능 저하를 유발하는 구현은 지양하고 대안을 제시할 것.
- 콘솔/로그에서 오류를 발견하면 "오류가 있다"고만 보고하지 말 것. (1) 왜 발생하는지 코드/씬 구조까지 추적 (2) 실제 문제를 구체적으로 짚고 (3) 해결 방법까지 제시. 고치지 않은 진단은 `.claude/docs/known-issues.md`에 기록(훅이 사용자 승인을 요구함).
- 슬래시 커맨드 없이 채팅으로 기능을 요청받아도, 코드를 쓰기 전에 ① 비슷한 기존 코드 검색 결과 ② Update/FixedUpdate 등 핫 루프 성능 영향 ③ SO/이벤트로 확장 가능한 지점, 이 세 가지를 한 줄씩 먼저 보여줄 것. (`workflow_view` 스킬이 이 흐름을 PRD까지 확장함)
- 씬(.unity)의 실제 GameObject 인스턴스는 프리팹과 다를 수 있음. 컴포넌트 존재 여부는 프리팹만 보고 판단하지 말고 필요하면 MCP로 실제 씬 인스턴스를 확인할 것.
- 기능 계획을 제시할 때는 관련 기존 필드/메서드/최적화를 구체적으로 인용할 것(예: "Laser.cs의 HitStep/MaxHitCount가 이미 이 역할을 한다").
- 서브에이전트는 사용자가 시키거나 커맨드가 명시할 때만. 기본은 이 세션이 직접 한다 (`unity-reviewer`는 독립 리뷰용으로 예외).

---

## 파이프라인

| 상황 | 쓰는 것 |
|---|---|
| 낮 · 새 기능 "~만들어줘" | `workflow_view` 스킬(자동 발동) → PRD 승인 → 지금 구현(`/unity-workflow`) 또는 밤 큐(`status: approved`) |
| 낮 · 여러 기능이 얽힌 프로젝트 단위 작업 | `unity-project-orchestrator` 스킬 → 계약/소유권 확정 → 최대 3개 모듈 병렬 구현(`unity-module-worker`) → 메인 MCP 통합 |
| 낮 · 버그 | `/unity-fix` |
| 낮 · 리뷰/테스트/빌드 | `/unity-review`, `/unity-test`, `/unity-build`, `/unity-ralph`(검증 루프) |
| **밤 · 무인** | `.claude/autopilot.ps1` → PRD마다 `claude -p "/unity-autopilot <prd>"` (새 컨텍스트, 질문 없음, `auto/<날짜>` 브랜치에 PRD당 커밋 1개). 아침에 `git log auto/*` + PRD `status` + `git stash list`(blocked) 확인 |

게이트(훅): `.unity/.prefab/.meta` 직접 편집 차단 · `UnityEditor` 가드 누락 차단 · 파괴적 git/rm 차단 · `git commit` 차단(autopilot 브랜치 제외) · `.cs` 편집 후 `cs-lint` 경고 · **응답 종료 시 `compile-gate`가 Unity 컴파일 에러를 확인해 에러가 있으면 계속 고치게 함**.

---

## 아키텍처

게임 시스템(BT, SO Action, Blackboard, Object Pool, C# 이벤트, R3 바인딩, UniTask, MVP UI, 입력 시스템) 규칙은 `.claude/rules/architecture.md`. 자체 asmdef는 아직 없음(Plugins의 UniTask만 있음).

**입력 결정(2026-09-14):** `InputManager` 싱글톤은 유지하되 내부를 `List<InputActionReference>`에서 **생성 C# 클래스(`PlayerAction`)**로 전환하기로 함 — 기존 `Assets/3D/06_Input/InputManager.cs` 마이그레이션은 별도 PRD.

---

## 컨벤션

- `.claude/rules/` 필수: `csharp-unity.md`(`m_` + 타입 헝가리안, 매개변수 `_` 접두사, 최소 가시성), `performance.md`, `serialization.md`, `unity-specifics.md`, `architecture.md`
- 컴포넌트 참조는 `Awake()`에서 캐싱, 핫 루프에서 `GetComponent` 금지
- 직렬화 에셋은 Unity YAML(Force Text)
- 기능 설계의 흐름/상태는 Mermaid로(`architecture.md`의 규칙)

---

## 스킬

프로젝트 전용: `object-pooling`(이 프로젝트 풀 구조 — Bullet/FX/Enemy 필수), `r3`, `workflow_view`, `unity-project-orchestrator`, `deep-interview`, `unity-mcp-patterns`. 패키지별: `unitask`, `dotween`, `addressables`, `input-system`, `cinemachine`, `navmesh`, `urp-pipeline`, `textmeshpro`, `animation`, `physics`, `event-systems`, `scriptable-objects`.

---

## 커스텀 노트

세션 중 발견했지만 고치지 않은 문제는 `.claude/docs/known-issues.md`. 작업 시작 전에 한 번 확인.
