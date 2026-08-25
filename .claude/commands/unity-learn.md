---
name: unity-learn
description: "누적된 세션 학습 내용을 검토하고, 반복되는 패턴을 추출하며, 세션 데이터로부터 새로운 스킬 초안을 작성합니다."
user-invocable: true
args: subcommand
---

# /unity-learn — 학습 파이프라인

누적된 세션 학습 내용을 관리하고 활용합니다: **$ARGUMENTS**

이 커맨드는 `auto-learn.sh` 훅(strict 프로필)이 각 세션 후 `.claude/state/learnings.jsonl`에 기록하는 세션 패턴 데이터를 다룹니다. v1.3.0 이전 프로젝트의 경우, 파일이 `.claude/learnings.jsonl`에 있을 수 있습니다.

## 서브커맨드

### `review` (기본값)

`.claude/state/learnings.jsonl`을 읽고 누적 데이터를 요약하는 대시보드를 제시합니다:

1. `.claude/state/learnings.jsonl`(v1.3.0 이전 프로젝트의 경우 대체 경로로 `.claude/learnings.jsonl`)에서 **학습 파일을 읽습니다**
2. **집계하여 제시합니다:**

```markdown
## Session Learning Dashboard

**Total sessions:** [count]
**Date range:** [earliest] to [latest]
**Total duration:** [hours]h [minutes]m

### File Activity
| Category | Total Edits | Sessions |
|----------|-------------|----------|
| Models   | [count]     | [count]  |
| Views    | [count]     | [count]  |
| Systems  | [count]     | [count]  |
| Tests    | [count]     | [count]  |
| Shaders  | [count]     | [count]  |
| Editor   | [count]     | [count]  |

### Session Categories
| Category     | Count | Avg Duration |
|-------------|-------|--------------|
| bug-fix     | [n]   | [m]m         |
| performance | [n]   | [m]m         |
| architecture| [n]   | [m]m         |
| workflow    | [n]   | [m]m         |
| integration | [n]   | [m]m         |

### Tool Usage
| Tool  | Total Calls | Avg per Session |
|-------|-------------|-----------------|
| Edit  | [count]     | [avg]           |
| Read  | [count]     | [avg]           |
| Bash  | [count]     | [avg]           |
| ...   | ...         | ...             |
```

### `extract`

학습 로그를 분석하여 반복 패턴을 찾고 신뢰도 점수를 적용합니다:

1. `.claude/state/learnings.jsonl`(대체 경로: `.claude/learnings.jsonl`)에서 **모든 항목을 읽습니다**
2. **카테고리별로 그룹화합니다** (bug-fix, performance, architecture, workflow, integration)
3. **반복 패턴을 식별합니다:**
   - 여러 세션에 걸쳐 등장하는 파일 → 핫스팟일 가능성이 높음
   - 지배적인 카테고리 → 프로젝트의 현재 집중 영역
   - 도구 사용 패턴 → 워크플로우 최적화 기회
4. **신뢰도 점수를 적용합니다:**
   - **높은 신뢰도** (3세션 이상): 패턴이 잘 정립되어 있으며, 실제 프로젝트 관례일 가능성이 높음
   - **중간 신뢰도** (2세션): 패턴이 형성되는 중이며, 주목할 가치는 있지만 우연일 수 있음
   - **낮은 신뢰도** (1세션): 단일 관찰이므로 기록은 하되 아직 행동으로 옮기지 않음
5. **결과를 제시합니다:**

```markdown
## Extracted Patterns

### High Confidence
- [pattern description] (seen in N sessions)

### Medium Confidence
- [pattern description] (seen in N sessions)

### Low Confidence
- [pattern description] (seen in 1 session)

### Hotspot Files
- [file path] — edited in N sessions

### Recommendations
- [actionable suggestion based on patterns]
```

### `draft-skill <topic>`

추출된 패턴으로부터 SKILL.md 초안을 생성합니다:

1. `<topic>`과 관련된 **학습 내용을 필터링합니다** (파일 경로, 카테고리, 도구 패턴에 대한 퍼지 매칭)
2. 반복 패턴을 하나의 응집된 스킬 문서로 **합성합니다**
3. 적절한 frontmatter를 갖춘 완전한 SKILL.md를 **생성합니다:**

```yaml
---
name: [derived-from-topic]
description: "[synthesized description from patterns]"
globs: ["[relevant file patterns]"]
---
```

4. 다음 안내와 함께 초안을 **출력합니다** (stdout):
   ```
   Draft skill generated. To install:
     1. Create directory: .claude/skills/core/[skill-name]/
     2. Save the above content to: .claude/skills/core/[skill-name]/SKILL.md
     3. Review and refine the content before use
   ```

### `analytics`

심층 세션 분석 — 학습 내용을 실행 가능한 지표와 추세로 집계합니다:

1. `.claude/state/learnings.jsonl`(v1.3.0 이전 프로젝트의 경우 대체 경로로 `.claude/learnings.jsonl`)에서 **모든 항목을 읽습니다**
2. **다음을 제시합니다:**

```markdown
### Session Analytics

**Time Analysis**
| Metric | Value |
|--------|-------|
| Total sessions | [count] |
| Total time | [hours]h [minutes]m |
| Avg session | [minutes]m |
| Longest session | [minutes]m |

**Agent Usage** (from agent_context data if available)
| Agent | Sessions | Avg Duration |
|-------|----------|--------------|
| [agent] | [count] | [minutes]m |

**Warning Hotspots** (from warnings_fired data if available)
| Warning | Count | Files |
|---------|-------|-------|
| [hook:message] | [count] | [affected files] |

**File Hotspots** (files edited across multiple sessions)
| File | Sessions | Category |
|------|----------|----------|
| [path] | [count] | [category] |

**Trends**
- Average session duration: [trending up/down/stable]
- Warning frequency: [trending up/down/stable]
- Most active category: [category]
```

3. **다음 행동을 제안합니다:**
   - 이 패턴들로부터 스킬을 생성하려면 `/unity-skillify <topic>`을 사용하십시오.
   - 패턴 신뢰도 수준을 확인하려면 `/unity-learn extract`를 사용하십시오.

## 규칙

- **기본적으로 읽기 전용** — `review`, `extract`, `analytics`는 오직 읽고 분석만 하며 절대 파일을 수정하지 않습니다
- **`draft-skill`은 출력만 하고 저장하지 않습니다** — 사용자가 직접 검토하고 파일을 배치해야 합니다
- **데이터가 없으면 출력도 없습니다** — `.claude/state/learnings.jsonl`(또는 `.claude/learnings.jsonl`)이 존재하지 않거나 비어 있으면 그 사실을 명확히 알리십시오
- **프라이버시** — 학습 내용은 프로젝트 로컬에 저장되며 절대 외부로 전송되지 않습니다
