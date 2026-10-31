# Unity Codex 작업 지침

이 파일은 새 Unity 프로젝트에 설치한 뒤 프로젝트의 실제 Unity 버전, 패키지, 플랫폼, 네이밍 규칙에 맞게 수정한다.

## 작업 원칙

- 기능 구현 전에 기존 유사 코드, Update/FixedUpdate 성능 영향, ScriptableObject·이벤트 확장 지점을 각각 한 줄로 보고한다.
- C# 수정 전에 `.codex/rules/`의 해당 규칙을 읽는다. 기존 구조와 명명법을 우선한다.
- Unity 씬·프리팹·직렬화 에셋·`.meta`는 텍스트로 직접 편집하지 않고 Unity MCP로 수정한다.
- 씬의 실제 인스턴스와 프리팹은 다를 수 있으므로 필요한 경우 둘 다 확인한다.
- 오류는 호출 경로와 씬 구성을 추적해 원인, 영향, 해결책을 설명한다.
- 매 프레임 할당과 컴포넌트 검색을 피하고, Unity 컴파일과 관련 테스트 결과를 보고한다.
- 서브에이전트는 사용자가 요청하거나 명시적으로 병렬 작업을 선택한 경우에만 사용한다.

## 도구와 스킬

- Unity MCP: `.codex/config.toml`의 `UnityMCP` URL을 현재 환경에 맞게 설정한다.
- Codex 스킬: `.agents/skills/`의 해당 `SKILL.md`를 읽는다.
- 큰 기능의 계획 리뷰는 `$workflow_view`, 국소 기능 구현은 `$unity-workflow`, 버그는 `$unity-fix`를 사용한다.
- 자동 실행: `.codex/autopilot.ps1`과 `$unity-autopilot`은 명시적으로 승인된 PRD에만 사용한다.
- 훅: `.codex/hooks.json`의 네 진입점이 편집 보호, 명령 보호, C# 경고, 컴파일 확인을 실행한다.

## 프로젝트별로 채울 항목

- Unity 버전과 대상 플랫폼:
- 설치한 패키지와 선택한 async·입력·반응형 라이브러리:
- C# 명명법과 아키텍처:
- 테스트 위치와 빌드 씬:
- PR/알림 운영 방식:
