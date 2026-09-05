---
name: unity-session-resume
description: "저장된 세션 스냅샷을 실시간 .claude/state/session.json으로 복원하여 다음 SessionStart가 이를 반영하도록 합니다."
user-invocable: true
args: label
---

# /unity-session-resume — 저장된 세션 재개

`/unity-session-save`로 생성된 스냅샷을 복원하여 다음 세션 복구 시 이 상태를 읽도록 합니다: **$ARGUMENTS**

## 단계

1. `$ARGUMENTS`에서 라벨을 파싱합니다. 없는 경우, `/unity-sessions`를 실행하고 사용자에게 하나를 선택하라고 안내합니다.
2. `.claude/state/sessions/<label>.json`이 존재하는지 확인합니다. 존재하지 않으면
   - 퍼지 매칭(접두어, 부분 문자열)을 찾아봅니다.
   - 매칭이 여러 개면, 이를 보여주고 중단합니다.
   - 매칭이 하나면, 사용하기 전에 확인을 받습니다.
3. 현재 `.claude/state/session.json`의 `saved_at`이 최근 30분 이내이고 수정된 파일이 있는 경우 사용자에게 경고합니다 — 실시간 작업 내용을 덮어쓸 수 있습니다. 먼저 `/unity-session-save current-<timestamp>`를 통해 현재 상태를 자동 저장할지 제안합니다.
4. `sessions/<label>.json`을 `session.json` 위에 복사합니다.
5. 복원된 내용을 보고합니다.

```markdown
Session **<label>** restored.

**Branch (snapshot):** <branch>
**Current branch:** <current>
**Workflow phase:** <workflow_phase>
**Plan:** <plan.description>
**Modified files at save:** <count>
**Saved:** <ISO timestamp>

⚠ Branch mismatch — run `git checkout <branch>` before resuming edits.
```

6. 메모리 내 대화 컨텍스트는 복원되지 않으며 상태 파일만 복원된다는 점을 사용자에게 알립니다 — SessionStart 훅이 실행되어 복원된 컨텍스트를 주입하려면 새 대화를 시작해야 합니다.

## 규칙

- **브랜치 불일치 경고** — 스냅샷의 `.branch`가 현재 브랜치와 다르면 이를 강조하고 checkout을 제안합니다.
- **TTL 준수** — 스냅샷이 `UNITY_SESSION_TTL_HOURS`(기본값 4)보다 오래된 경우 경고하되 허용합니다.
- **원자적 처리** — `session.json.tmp`에 쓴 다음 `mv`하여 충돌이 발생해도 파일이 절반만 작성된 상태로 남지 않도록 합니다.
- **조용한 덮어쓰기 금지** — 항상 무엇이 대체되는지 보여줍니다.
