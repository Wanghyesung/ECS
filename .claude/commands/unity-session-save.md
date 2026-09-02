---
name: unity-session-save
description: "현재 세션 상태를 이름표가 붙은 스냅샷으로 .claude/state/sessions/<label>.json에 저장하여 이후 /unity-session-resume에서 사용할 수 있게 합니다."
user-invocable: true
args: label
---

# /unity-session-save — 현재 세션 스냅샷 저장

살아있는 세션 상태(`.claude/state/session.json`)를 이름이 지정된 스냅샷으로 저장합니다: **$ARGUMENTS**

`session.json`을 덮어쓰는 자동 Stop-hook 저장과 달리, 이 명령은 사용자가 나중에 돌아올 수 있는 이름 붙은 사본을 생성합니다.

## 단계

1. `$ARGUMENTS`에서 레이블을 확인합니다.
   - 비어 있으면: 기본값으로 `auto-<YYYYMMDD-HHMM>`을 사용합니다.
   - `[a-z0-9-]+` 형식으로 정리(sanitize)하고, 슬래시나 공백이 포함된 값은 거부합니다.
2. `.claude/state/session.json`이 존재하는지 확인합니다. 존재하지 않으면 사용자에게 메모리 상의 컨텍스트를 저장할지 물어봅니다(이 경우 먼저 Stop-hook과 동등한 저장을 트리거하거나, 최소 한 번의 Edit/Read가 일어날 때까지 기다리라고 안내합니다).
3. `.claude/state/sessions/`가 존재하는지 확인합니다.
4. `session.json`을 읽고, 사본에 최상위 `label` 필드와 `source: "manual-save"` 필드를 추가합니다.
5. `.claude/state/sessions/<label>.json`에 씁니다.
6. 동일한 레이블의 파일이 이미 존재하면 덮어쓰기 전에 사용자에게 확인합니다. 사용자가 거부하면 기본적으로 숫자 접미사(`<label>-2.json`)를 붙입니다.
7. 다음과 같이 보고합니다:

```markdown
Session saved as **<label>**.
Branch: <branch>
Phase: <workflow_phase>
Modified files: <count>
Use `/unity-session-resume <label>` to restore.
```

## 규칙

- **레이블 정리(sanitization)** — `..`, 선행 `-`, 경로는 거부하며 `[a-z0-9-]+`만 허용합니다.
- **절대 조용히 덮어쓰지 않습니다** — 항상 확인을 요청하고, 항상 안전한 대안을 제공합니다.
- **멱등성(Idempotent)** — 사용자 승인을 받아 같은 레이블을 두 번 저장하면 깔끔하게 대체됩니다.
- **살아있는 세션을 변경하지 않습니다** — `session.json`은 그대로 유지됩니다.
