---
name: unity-skill-stocktake
description: "중복, 오래된 참조, 한 번도 로드되지 않은 항목, 깨진 글롭(glob), 프론트매터 문제에 대해 .claude/ 내의 모든 스킬과 에이전트를 감사합니다. 아무것도 수정하지 않고 정리 보고서를 생성합니다."
user-invocable: true
args: none
---

# /unity-skill-stocktake — 메타 유지보수 감사

`.claude/skills/`와 `.claude/agents/`에서 위생 문제를 스캔합니다. 이것은 읽기 전용 감사이며 — 출력물은 사용자가 조치를 취할 수 있는 보고서입니다.

## 단계

### 1. 항목 열거

- 스킬: `.claude/skills/**/SKILL.md` (재귀적; 상위 디렉터리별로 분류).
- 에이전트: `.claude/agents/*.md`.
- 커맨드: `.claude/commands/*.md` (스킬이 참조되고 있는지 확인하기 위한 교차 참조용).

각 파일에 대해, 라인 스캔을 통해 YAML 프론트매터를 파싱합니다(첫 번째 `---` 라인 쌍 사이의 모든 내용).

### 2. 프론트매터 검증

필수 필드가 누락된 스킬/에이전트/커맨드를 표시합니다:

- 스킬: `name`, `description`. `alwaysApply`와 `globs`는 선택 사항이지만 `globs`는 권장됩니다.
- 에이전트: `name`, `description`, `model`, `tools`. `color`는 선택 사항.
- 커맨드: `name`, `description`.

파일명 stem과 일치하지 않는 `name`을 표시합니다.

### 3. 중복 / 유사 중복 탐지

2단계로 진행합니다:

1. 항목 간 완전히 동일한 `name` → 심각한 오류.
2. 설명 유사도 — 같은 카테고리 내 모든 설명 쌍 간의 토큰 중첩(소문자화, 비단어 문자로 분리)을 계산합니다. 60% 이상 중첩되거나 첫 문장이 동일하게 시작하는 쌍을 표시합니다.

표시할 예시:
> `skills/gameplay/object-pooling`과 `skills/gameplay/pool-allocator` — 설명이 18개 토큰 중 14개를 공유합니다; 병합을 고려하세요.

### 4. 글롭(Glob) 도달 가능성

모든 스킬의 각 `globs:` 항목에 대해, `.claude/` 내부가 아닌 프로젝트 루트 아래에 일치하는 파일이 *하나라도* 존재하는지 확인합니다.

- 일치하는 항목이 0개인 글롭 → "오래된 글롭" 경고.
- `.claude/` 자체만 일치하는 글롭 → 메타 스킬일 가능성이 높음; 경고는 아니지만 기록해 둡니다.

### 5. 참조 감사

- 각 스킬에 대해, 그 `name`이 어떤 에이전트(`skills:` 필드)나 커맨드(마크다운 본문)에서 참조되는지 확인합니다. 한 번도 참조되지 않았고 실제 파일과 일치하는 `globs:`도 없는 스킬은 제거 후보입니다.
- 각 에이전트에 대해, 어떤 커맨드에서 참조되는지 확인합니다. 참조되지 않는 에이전트는 (사용자가 직접 호출할 수 있으므로) 허용되지만 목록에는 기재할 가치가 있습니다.

### 6. 로드 이력 감사 (최선 노력 기준)

`.claude/state/learnings.jsonl` 또는 `.claude/state/instincts/observations.jsonl`이 존재하면, 각 스킬이 최근 N개 세션(기본값 N = 10) 동안 언급되었거나 자동 로드되었는지 확인합니다. 최근 로드가 0회인 스킬은 강등 또는 삭제 후보입니다.

learnings 파일이 존재하지 않으면 이 단계를 건너뛰고 그 사실을 기록합니다.

### 7. 오래된 코드 참조

각 스킬/에이전트에 대해 본문을 스캔하여 다음을 찾습니다:
- 더 이상 존재하지 않는 파일 경로(`.claude/...` 또는 `Assets/...`).
- 더 이상 사용되지 않게(deprecated) 되었을 수 있는 API 참조(Unity 네임스페이스) (로컬 코드에 `[Obsolete]` 패턴이 나타나는 경우에만 표시).

차단성 아님; "수동 검토 필요"로 표시합니다.

## 보고서 형식

```markdown
# Skill Stocktake — <date>

**Scanned:** <n> skills, <n> agents, <n> commands

## Frontmatter issues (<count>)
- `skills/<path>/SKILL.md` — missing `description`
- `agents/<name>.md` — `model` is `haiku` but tools include MCP writes (potential mismatch)

## Duplicates / near-duplicates (<count>)
- ...

## Stale globs (<count>)
- `skills/platform/mobile-input` — globs `["Assets/Input/**/*.cs"]` match 0 files

## Never-referenced skills (<count>)
- `skills/<name>` — not referenced by any agent/command and globs match 0 files

## Orphaned agents (<count>)
- `agents/<name>` — not referenced from any command (informational)

## Unused in recent sessions (<count>, last 10 sessions)
- `skills/<name>` — 0 loads; `skills/<name>` — 0 loads

## Stale code refs (<count>)
- ...

## Summary
- Remove candidates: <list>
- Merge candidates: <pairs>
- Needs review: <list>
```

다음 문구로 마무리합니다:

> 적용된 변경 사항이 없습니다. 각 후보를 검토한 뒤 수동으로 제거/병합하거나, `/unity-skillify`를 실행하여 통합하세요.

## 규칙

- **읽기 전용입니다.** 파일을 절대 수정, 삭제, 이동하지 않습니다.
- **부드럽게 실패합니다.** YAML 블록이 잘못된 형식이면 해당 파일을 "파싱 불가"로 표시하고 계속 진행합니다.
- **효율적으로 동작합니다.** 가능하면 모든 파일을 전부 읽기보다 `grep -l`과 `find`를 사용합니다.
- **자동으로 수정하지 않습니다.** 어떤 후보에 조치를 취할지는 사용자가 결정합니다.
