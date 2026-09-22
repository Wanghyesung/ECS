# 하네스 이전 목록

다른 Unity 저장소에서 동일한 병렬 오케스트레이션을 사용하려면 아래 파일을 가져간다.

## 필수

- `.claude/skills/unity-project-orchestrator/**`
- `.claude/agents/unity-module-worker.md`
- `.claude/docs/orchestration/templates/**`
- `CLAUDE.md`의 프로젝트 단위 작업 라우팅 한 줄

## 대상 프로젝트에 맞게 수정

- Unity 버전과 사용 패키지
- C# 네이밍 및 직렬화 규칙 경로 (`.claude/rules/`)
- MCP 서버 이름과 URL (`.claude/settings.json`의 `mcpServers`, `permissions.allow`의 `mcp__UnityMCP__*`)
- 컴파일·테스트 명령
- 메인 전용 경로와 공용 계약 위치
- 최대 병렬 작업 수 (Claude는 설정 항목이 없으므로 SKILL.md의 "한 메시지에 최대 세 개" 문구)
- `unity-module-worker.md`의 `tools:` 목록 (MCP 도구 이름은 서버마다 다름)

## 선택

- `.claude/agents/unity-reviewer.md`
- `.claude/agents/unity-verifier.md`
- `unity-review`, `unity-test`, `unity-mcp-patterns` 스킬/커맨드

## 가져가지 않을 것

- `.claude/docs/orchestration/<project-id>/**` (실제 작업 패키지)
- 현재 게임 전용 PRD와 known issues
- 다른 프로젝트에 존재하지 않는 에셋 경로

## Codex와의 대응

같은 구조가 `.agents/skills/unity-project-orchestrator/` + `.codex/agents/unity-module-worker.toml` + `.codex/docs/orchestration/`에 Codex용으로 있다. 한쪽을 고치면 다른 쪽도 맞춘다.

새 프로젝트에서 첫 실행은 작은 수직 슬라이스로 하고, `Test-OrchestrationPackage.ps1`로 문서 소유권을 먼저 검증한다.
