---
name: unity-sessions
description: "저장된 모든 세션을 브랜치, 단계, 수정된 파일 수, 타임스탬프와 함께 나열합니다. 세션은 /unity-session-save로 생성된 .claude/state/sessions/ 내의 스냅샷입니다."
user-invocable: true
args: none
---

# /unity-sessions — 저장된 세션 목록 보기

어떤 세션을 재개할지 결정할 수 있도록 충분한 컨텍스트와 함께 저장된 모든 세션 스냅샷을 보여줍니다.

## 단계

1. `.claude/state/sessions/` 디렉터리를 읽습니다(없으면 생성한 뒤 "저장된 세션이 아직 없습니다"라고 보고합니다).
2. 각 `*.json` 파일에 대해 다음을 읽습니다:
   - `label` (파일 이름의 stem에서 가져옴)
   - `branch`, `workflow_phase`, `saved_at` (ISO8601)
   - `tool_calls`, `warnings_count`
   - `modified_files` 개수
   - `plan.description`이 있으면 함께
3. `saved_at` 기준 내림차순으로 정렬합니다.
4. 표를 렌더링합니다:

```markdown
## Saved Sessions

| Label | Branch | Phase | Age | Files | Plan |
|---|---|---|---|---|---|
| refactor-hud | feature/hud | Execute | 2h ago | 14 | Rewrite HUD to UI Toolkit |
| perf-spike   | main        | Verify  | 1d ago | 3  | Pool bullet VFX |
| ... | ... | ... | ... | ... | ... |
```

5. 다음 명령을 제안합니다:

> `/unity-session-resume <label>`로 세션을 재개하거나 새로 시작하세요 — 재개하기 전까지 현재 세션에는 아무 영향이 없습니다.

## 규칙

- **읽기 전용입니다.** 세션 파일을 절대 삭제하거나 수정하지 않습니다.
- **디렉터리가 없는 경우를 우아하게 처리합니다** — "저장된 세션이 아직 없습니다"라고 말하고 `/unity-session-save`를 안내합니다.
- **잘못된 형식의 JSON 파일은 무시합니다** — 하단에 건너뛴 파일 수를 보고하되 실패로 처리하지 않습니다.
