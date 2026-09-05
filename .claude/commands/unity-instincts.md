---
name: unity-instincts
description: "프로젝트의 본능(instinct) 라이브러리를 관리합니다 — PostToolUse 관찰을 통해 자동으로 수집된 원자적 학습 행동을 조회, 승격, 진화, 내보내기, 가져오기합니다."
user-invocable: true
args: subcommand
---

# /unity-instincts — 본능(Instinct) 라이브러리

Unity 특화 원자적 학습 내용을 관리합니다: **$ARGUMENTS**

본능(instinct)은 `instinct-capture.sh`(PostToolUse)가 수집하고 `instinct-distill.sh`(Stop)가 정제하는 원자적 학습 행동입니다. 각 본능은 트리거(trigger), 액션(action), 신뢰도 점수(0.3–0.9), 증거 횟수(evidence count), 스코프(프로젝트 또는 전역)를 가집니다.

저장소:
- 프로젝트 본능: `.claude/state/instincts/project/<project-hash>/*.json`
- 전역 본능:  `.claude/state/instincts/global/*.json`
- 원본 관찰 기록:  `.claude/state/instincts/observations.jsonl`

## 서브커맨드

### `status` (기본값)

현재 프로젝트의 본능 라이브러리 대시보드를 표시합니다.

1. 프로젝트 해시 계산: `git config --get remote.origin.url | shasum | head -c 12` (실패 시 `git rev-parse --show-toplevel`로 대체).
2. `.claude/state/instincts/project/<hash>/` 및 `.claude/state/instincts/global/` 아래의 파일 목록을 가져옵니다.
3. 각 본능에 대해 JSON 필드를 읽고 다음과 같이 렌더링합니다:

```markdown
## Instinct Library

**Project:** <project-hash>
**Project instincts:** <count> (<count-above-0.7> high-confidence)
**Global instincts:** <count>
**Raw observations:** <count> (this session: <count>)

### High confidence (>= 0.7) — project
| Trigger | Action | Confidence | Evidence |
|---|---|---|---|
| before editing *View.cs | expect quality-gate warnings; read Model first | 0.8 | 12 |
| ... | ... | ... | ... |

### Medium (0.5–0.69) — project
...

### Low (0.3–0.49) — project
...

### Global (all scopes)
...
```

각 구간 내에서는 `evidence_count` 내림차순으로 정렬합니다.

### `list [--domain <d>] [--min-confidence <n>]`

본능을 필터링하여 나열합니다. 라이브러리가 커질 때 유용합니다:

```bash
/unity-instincts list --domain mvp
/unity-instincts list --min-confidence 0.7
```

JSON 파일을 읽고 필터링한 뒤, 필터링된 집합에 한해 `status`와 동일한 표 형식으로 출력합니다.

### `evolve [--min-confidence 0.8]`

고신뢰도 본능을 초안 스킬 파일로 승격시킵니다.

1. 프로젝트 및 전역 스코프에서 `confidence >= N`(기본값 0.8) 이면서 `evidence_count >= 5`인 본능을 수집합니다.
2. `domain`별로 그룹화합니다.
3. 각 도메인 클러스터에 대해 SKILL.md 초안을 작성합니다:

```yaml
---
name: learned-<domain>-patterns
description: "Patterns auto-learned from Unity sessions in this project"
alwaysApply: false
globs: ["Assets/Scripts/**/*.cs"]
---

# Learned <Domain> Patterns

## Trigger
<trigger from highest-confidence instinct>

## Patterns
- <trigger>: <action> (seen <n> times)
- ...

## Source
Evolved from instincts: <list of instinct ids>
```

4. 초안을 stdout으로 출력만 하고, 파일로 저장하지 마십시오. 사용자에게 다음과 같이 안내합니다:
   > `.claude/skills/core/learned-<domain>-patterns/SKILL.md`에 스킬 초안이 생성되었습니다 — 설치하기 전에 검토하세요.

### `promote <instinct-id> [--force]`

프로젝트 본능을 전역 스코프로 이동시킵니다. 일반적으로는 동일한 트리거/액션이 2개 이상의 프로젝트에서 관찰된 경우에만 수행하지만, `--force`를 사용하면 이 검사를 건너뜁니다.

1. `.claude/state/instincts/project/<project-hash>/<instinct-id>.json`을 읽습니다.
2. `--force`가 없는 경우: 다른 프로젝트 해시에서 동일한 트리거를 찾습니다. 발견되지 않으면 거부하고 그 사실을 알립니다.
3. `scope: "global"`로 재작성하고 `project_id`를 제거한 뒤, `.claude/state/instincts/global/<instinct-id>.json`에 기록합니다.
4. 프로젝트 사본을 삭제합니다.

### `demote <instinct-id>`

promote의 반대 동작입니다. 전역 본능이 실제로는 프로젝트 특화 사항이었던 것으로 판명된 경우, 이를 현재 프로젝트로 다시 이동시킵니다.

### `export [--out <file>]`

모든 본능(프로젝트 + 전역)을 하나의 JSON 파일로 내보냅니다. 기본 출력 위치: `.claude/state/instincts/export-<date>.json`.

### `import <file>`

`export`로 생성된 파일에서 본능을 가져옵니다. 중복 ID는 병합됩니다: `evidence_count`는 합산되고, `confidence`는 최댓값을 취하며, `last_seen`은 최댓값을 취합니다.

### `clear [--project | --global | --observations | --all]`

상태를 삭제합니다. 실수로 인한 초기화를 막기 위해 명시적인 플래그가 필요합니다.

- `--project`: 현재 프로젝트의 본능만 제거
- `--global`: 전역 본능 제거
- `--observations`: 원본 observations.jsonl 제거
- `--all`: 위의 모든 것

실행 전 확인을 요청하십시오.

## 규칙

- **기본적으로 읽기 전용** — `status`, `list`, `evolve`, `export`는 본능 파일을 절대 수정하지 않습니다.
- **`evolve`는 출력만 하고 저장하지 않습니다** — 초안은 stdout으로만 나가며, 사용자가 직접 설치합니다.
- **promote는 프로젝트 간 관계를 인식합니다** — `--force`가 없으면 여러 프로젝트에서의 증거 없이는 승격을 거부합니다.
- **관찰 기록은 원본 데이터입니다** — distiller의 근거가 되는 소스이므로 수동으로 편집하지 마십시오.
- **프라이버시** — 본능은 기본적으로 프로젝트 로컬에 저장되며 절대 컴퓨터 밖으로 나가지 않습니다.

## 사용 시점

- 주기적으로(`status`) 툴킷이 당신의 Unity 워크플로우에 대해 무엇을 학습했는지 확인할 때.
- 긴 세션 후(`evolve`) 학습 내용이 규칙이나 스킬로 인코딩할 만한 것으로 결정화되었는지 확인할 때.
- 여러 프로젝트에 걸쳐(`promote`) 동일한 패턴이 어디서나 통하는 것을 발견했을 때.
