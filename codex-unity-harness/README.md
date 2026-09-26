# 재사용 가능한 Unity Codex 하네스

현재 ECS 저장소의 Claude → Codex 이전 결과 중 다른 Unity 저장소에서도 쓸 수 있는 기술 설정을 분리한 템플릿입니다. 이 폴더는 원본 프로젝트의 실행 설정을 바꾸지 않습니다.

## 구성

- `template/AGENTS.md`: 새 프로젝트용 작업 지침 골격
- `template/.codex/config.toml`: Unity MCP와 에이전트 역할
- `template/.codex/hooks.json`, `hooks/`: Codex 훅 네 진입점과 검사 스크립트
- `template/.codex/rules/`: 프로젝트 고유 네이밍·아키텍처를 뺀 Unity 기본 규칙
- `template/.codex/autopilot.ps1`: 승인된 PRD용 Codex 무인 실행
- `template/.codex/agents/`: 역할 정의
- `template/.codex/docs/orchestration/templates/`: 병렬 작업 문서 양식
- `template/.agents/skills/`: Unity 도구·패키지별 스킬과 범용 PRD·커밋 절차

게임 기획, 밸런스, 현재 프로젝트의 PRD·known issues, 전용 ObjectPool 구조, 개인 GitHub/Slack 게시 설정은 포함하지 않았습니다. 라이브러리별 스킬은 해당 패키지가 설치된 프로젝트에서만 적용하세요. 범용 `workflow_view`는 로컬 PRD와 검증까지 다루며 외부 게시는 자동으로 활성화하지 않습니다.

## 설치

대상은 `Assets`, `ProjectSettings`, `.git`이 있는 Unity 저장소 루트여야 합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\codex-unity-harness\Install-CodexUnityHarness.ps1 -ProjectPath 'C:\path\to\UnityProject'
```

기존 파일은 기본적으로 건너뜁니다. 이미 설치한 템플릿을 명시적으로 덮어쓸 때만 `-Force`를 사용하세요. 설치 뒤 `AGENTS.md`의 프로젝트 항목과 `.codex/config.toml`의 Unity MCP 주소를 수정합니다. 자동 실행이나 병렬 에이전트 사용은 각 프로젝트에서 별도로 결정합니다.

훅 실행에는 Git Bash와 `jq`가 필요합니다. Windows에서는 `bash.exe`가 PATH에 있어야 합니다. Codex에서 프로젝트 설정과 훅 신뢰를 승인한 뒤 다음으로 합성 입력 검사를 실행할 수 있습니다.

```powershell
bash .codex/hooks/test-codex-hooks.sh
powershell -NoProfile -ExecutionPolicy Bypass -File .codex/autopilot.ps1 -DryRun
```

훅은 `apply_patch`와 `exec_command` 입력을 검사하지만, 모든 셸 파일 쓰기를 완전히 차단하지는 못합니다. Unity MCP가 응답하지 않으면 종료 시 컴파일 훅은 경고 후 통과하므로 실제 컴파일을 별도로 확인하세요.
