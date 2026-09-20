# AGENTS.md - ECS Unity 프로젝트 컨텍스트

## 프로젝트

- Unity `2022.3.62f2`, URP. 주 타겟 Windows, 부 타겟 Android.
- Addressables, AI Navigation, Cinemachine, Input System, TextMeshPro, Test Framework, Unity MCP를 사용한다.
- 비동기는 UniTask만 사용하고 코루틴은 쓰지 않는다. Reactive 표준은 R3이며 UniRx는 사용하지 않는다. R3 코드를 쓰기 전 `Packages/manifest.json` 설치 여부를 확인한다.
- DOTween 트윈은 소유 오브젝트가 파괴될 때 종료한다.
- VContainer, MessagePipe, 레거시 Input은 사용하지 않는다.
- 게임 기획은 `.codex/docs/game-design.md`, 밸런스는 `.codex/docs/balance-guide.md`를 참고한다.

## 항상 지킬 규칙

- 기존 구조와 사용자 변경을 보존한다. 성능상 명확한 이득이 있을 때만 구조 변경을 제안한다.
- Update/FixedUpdate/LateUpdate에서 할당, 컴포넌트 탐색, LINQ를 만들지 않는다.
- 필수 `[SerializeField]`, `[RequireComponent]` 컴포넌트, 부트스트랩 이후 생존이 보장된 싱글톤은 호출부마다 null 방어하지 않는다. 잘못된 설정은 즉시 예외로 드러나게 한다.
- 런타임에 선택적이거나 실제로 사라질 수 있는 참조, 풀 고갈 결과, 비동기 취소 상태만 방어한다. 필수 컴포넌트를 런타임 `AddComponent`로 복구하지 않는다.
- bool 조건은 `!value` 대신 `value == false`, 긍정 조건은 필요하면 `value == true`로 쓴다. 새 코드와 이번 수정 범위에 적용하며 외부 플러그인 코드는 일괄 변경하지 않는다.
- 씬의 실제 인스턴스는 프리팹과 다를 수 있다. 필요하면 Unity MCP로 씬 인스턴스를 확인하며 `.unity/.prefab/.meta`를 직접 편집하지 않는다.
- 오류는 원인과 실제 영향까지 추적하고 해결책을 제시한다. 고치지 않은 문제를 `.codex/docs/known-issues.md`에 기록할 때는 사용자 승인을 받는다.
- 서브에이전트는 사용자가 요청하거나 선택한 스킬이 명시할 때만 쓴다. 독립 리뷰도 고위험 변경이나 명시적 요청에서만 수행한다.

## 작업 라우팅

- 일반 기능/국소 리팩터링: 이 세션이 직접 조사→구현→검증한다. 코드 전 3줄 사전조사만 제시한다.
- 전체 구조 감사, 싱글톤/DI, 책임·수명 경계 재설계: `$unity-architecture-refactor`.
- 대형 기능의 PRD·실제 코드 설계·PR·Slack 리뷰가 필요할 때만 사용자가 `$workflow_view`를 명시 호출한다.
- 버그 수정: `$unity-fix`. 리뷰/테스트/빌드/검증 루프: 각각 `$unity-review`, `$unity-test`, `$unity-build`, `$unity-ralph`.
- 밤 무인 실행: 승인된 PRD만 `.codex/autopilot.ps1`과 `$unity-autopilot`로 처리한다.

## 코드 수정 전

다음 세 가지를 한 줄씩 먼저 보여준다.

1. 비슷한 기존 코드와 재사용할 필드/메서드
2. Update/FixedUpdate 등 핫 루프 영향
3. SO/이벤트 확장 지점

C# 작업은 필요한 규칙만 읽는다.

- 항상: `.codex/rules/csharp-unity.md`, `.codex/rules/unity-specifics.md`
- 핫 루프·풀·렌더링: `performance.md`
- 직렬화 필드·SO·프리팹 데이터: `serialization.md`
- 새 시스템·책임 재배치·입력/R3/UniTask 구조: `architecture.md`
- 버그이거나 수정 영역과 관련된 항목이 있을 때만 `.codex/docs/known-issues.md`를 읽는다.

## 검증과 설정

- C# 수정 후 Unity 컴파일 오류를 확인하고 관련 테스트를 실행한다. 훅은 파괴적 Git 명령, 직접 씬/프리팹/meta 편집, UnityEditor 가드 누락을 차단한다.
- 프로젝트 설정/MCP: `.codex/config.toml`; 역할: `.codex/agents/*.toml`; 스킬: `.agents/skills/<name>/SKILL.md`.
- 상세 아키텍처와 패키지 규칙은 관련 Skill과 `.codex/rules/`에서 필요할 때만 읽는다.
