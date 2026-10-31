# 하네스 이전 목록

다른 Unity 저장소에서 동일한 병렬 오케스트레이션을 사용하려면 아래 파일을 가져간다.

## 필수

- `.agents/skills/unity-project-orchestrator/**`
- `.codex/agents/unity-module-worker.toml`
- `.codex/docs/orchestration/templates/**`
- `.codex/config.toml`의 `[agents]` 및 `[agents.unity-module-worker]`
- `AGENTS.md`의 프로젝트 단위 작업 라우팅 한 줄

## 대상 프로젝트에 맞게 수정

- Unity 버전과 사용 패키지
- C# 네이밍 및 직렬화 규칙 경로
- MCP 서버 이름과 URL
- 컴파일·테스트 명령
- 메인 전용 경로와 공용 계약 위치
- 최대 병렬 작업 수

## 선택

- `.codex/agents/unity-reviewer.toml`
- `.codex/agents/unity-verifier.toml`
- `unity-review`, `unity-test`, `unity-mcp-patterns` 스킬

## 가져가지 않을 것

- `.codex/docs/orchestration/examples/**`
- 현재 게임 전용 PRD와 known issues
- 다른 프로젝트에 존재하지 않는 에셋 경로

새 프로젝트에서 첫 실행은 예제 또는 작은 수직 슬라이스로 하고, `Test-OrchestrationPackage.ps1`로 문서 소유권을 먼저 검증한다.
