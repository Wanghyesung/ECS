# Codex 이전 기록

다른 Unity 프로젝트에 재사용할 Codex 기술 설정은 루트 [`codex-unity-harness/`](../codex-unity-harness/README.md)에 별도 템플릿과 설치 스크립트로 정리했다. 이 프로젝트의 게임 기획·밸런스·개인 PR/Slack 설정은 템플릿에 포함하지 않는다.

## 추가 설정: workflow_view GitHub PR 리뷰

대화형 `workflow_view`는 이제 검증 후 `awaiting-review`로 두고 GitHub PR을 생성/갱신한다. 설정은 `.codex/review-workflow.json`, 절차는 `.agents/skills/workflow_view/references/pr-review.md`에 있다. 지정된 Slack 대상에게 PR 링크를 알리고 사용자 승인/병합 확인 후 `done`으로 변경한다. `codex/*` 브랜치의 `git -c codex.workflow=pr-review commit --file ".codex/state/commit-message.txt"`만 일반 커밋 금지의 예외로 허용한다. 기존 야간 autopilot의 실행·결과 규칙은 유지한다.

현재 연결 준비: Slack 연결의 현재 사용자 `wanghyesung` DM(`U0BS15X072M`)을 `slack_destination`으로 설정했다. Git 원격 조회와 push dry-run은 성공했다. PR API는 연결된 GitHub 도구를 우선 사용하고, 없으면 `gh auth login`된 GitHub CLI를 사용한다. 현재 작업에는 GitHub 연결이 설치되어 있지 않고 CLI도 로그인되지 않았다. 실제 PR 생성/Slack 전송은 실행하지 않았으며 훅의 합성 입력 테스트로 허용·차단 동작을 검증했다.

## 이전 구조

프로젝트 지침은 루트 `AGENTS.md`, 설정은 `.codex/config.toml`, 스킬은 `.agents/skills/`에서 관리한다. 원본 `.claude/`와 `claude.md`는 보관했다.

| 원본 | Codex 위치 |
|---|---|
| claude.md | AGENTS.md |
| .claude/docs, rules | .codex/docs, rules |
| .claude/skills | .agents/skills (기존 18개 유지) |
| .claude/commands/*.md | .agents/skills/unity-*/SKILL.md (8개) |
| .claude/agents/*.md | .codex/agents/*.toml + config.toml 역할 등록 |
| .claude/settings.json MCP | .codex/config.toml의 UnityMCP |
| .claude/settings.json hooks | .codex/hooks.json + hooks/codex-hook.sh |
| .claude/autopilot.ps1 | .codex/autopilot.ps1 |

## 동작 차이

- `$unity-fix`, `$unity-review`, `$unity-test`, `$unity-build`, `$unity-optimize`, `$unity-workflow`, `$unity-ralph`, `$unity-autopilot`으로 호출한다. 새 스킬/역할은 새 Codex 작업에서 확인한다.
- 프로젝트 설정과 훅은 Codex의 프로젝트/훅 신뢰 절차를 따른다. 이 이전은 신뢰 기록이나 전역 권한을 변경하지 않는다.
- 훅은 Git Bash + `jq`를 재사용한다. Windows 실행 경로는 `C:/Program Files/Git/bin/bash.exe`이며 다른 설치 위치이면 `hooks.json`의 `commandWindows`를 수정한다. 제한 시간은 초 단위이다.
- 편집 훅은 Codex의 다중 파일 `apply_patch`를 파일별 입력으로 변환한다. C# 가드와 lint는 편집 조각 기반 휴리스틱이며 전체 C# 파서를 대체하지 않는다. 셸로 파일을 쓰는 모든 경우까지 탐지하지는 않으므로 AGENTS.md의 직접 편집 금지를 함께 따른다.
- `PreToolUse.permissionDecision=ask`는 현재 지원되지 않는다. known-issues 승인 규칙은 AGENTS.md와 훅의 안내로 유지하며 자동 승인 창이 뜬다고 가정하지 않는다.
- Claude 전용 `permissions.allow/defaultMode`, 로컬 설정과 모델명(opus/sonnet)은 복사하지 않는다. Codex의 기존 승인/샌드박스와 모델 선택을 유지한다.
- 설계는 로컬 Markdown과 Mermaid로 제시한다. Claude의 계획 모드 전환 도구나 외부 Artifact 게시 도구를 필수로 가정하지 않는다.
- compile-gate는 Unity MCP URL을 `.codex/config.toml`에서 읽는다(`UNITY_MCP_URL`로 재정의 가능). MCP 연결 실패 시 경고하며 검증 성공으로 간주하지 않는다.

## 무인 실행

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .codex/autopilot.ps1 -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File .codex/autopilot.ps1
# 원격 push와 PR 생성까지 원할 때만:
powershell -NoProfile -ExecutionPolicy Bypass -File .codex/autopilot.ps1 -CreatePullRequest
```

승인된 PRD마다 `codex exec`를 실행한다. 실제 실행 전 작업 트리가 깨끗해야 하고, 신뢰된 훅과 Unity MCP가 필요하다. `-DryRun`은 프로세스·네트워크·브랜치·로그를 만들지 않는다. 공유 Unity 에디터 충돌을 피하려고 프로젝트는 순차 실행한다. 기존 `-Projects`, 선택적 `-SlackWebhook`을 지원하며 `-BudgetUsd`는 대응하는 Codex 옵션이 없어 지원하지 않는다. 비용 상한을 보장하지 않는다. 샌드박스/승인은 우회하지 않으므로 커밋 등 권한이 부족한 경우 실패로 남기고 중단한다.

근거: [Codex 훅](https://learn.chatgpt.com/docs/hooks), [설정 참조](https://learn.chatgpt.com/docs/config-file/config-reference), [스킬](https://learn.chatgpt.com/docs/build-skills). 로컬 CLI `codex 0.155.0-alpha.2.6 exec --help`로 실행 옵션을 확인했다.
