---
name: unity-team
description: "병렬 에이전트 오케스트레이션 — 더 빠른 개발을 위해 여러 전문 에이전트를 동시에 실행합니다. 프리셋 팀과 커스텀 조합을 지원합니다."
user-invocable: true
args: team_spec
---

# /unity-team — 병렬 에이전트 오케스트레이션

다음 작업을 위해 여러 에이전트를 병렬로 실행합니다: **$ARGUMENTS**

이 커맨드는 순차 실행 대신 2~3개의 에이전트를 동시에 실행하여, 서로 다른 관심사를 독립적으로 처리할 수 있는 작업 흐름의 속도를 크게 높입니다.

## 팀 프리셋

`$ARGUMENTS`에서 팀 플래그를 파싱합니다. 플래그 이후의 모든 텍스트는 작업 설명입니다.

| 플래그 | 에이전트 | 최적 용도 |
|------|--------|----------|
| `--build` | unity-coder + unity-test-runner + unity-reviewer | 완전한 품질 커버리지를 갖춘 신규 기능 |
| `--feature` | unity-coder + unity-scene-builder + unity-test-runner | 씬 설정이 필요한 기능 |
| `--quality` | unity-reviewer + unity-optimizer + unity-test-runner | 기존 코드 감사 |
| `--security` | unity-security-reviewer + unity-reviewer + unity-linter | 코드 품질 점검을 포함한 보안 감사 |
| `--custom <agents>` | 콤마로 구분된 에이전트 이름 | 임의의 조합 |

### 퀵 모드

임의의 프리셋에 `--quick`을 추가하면 opus 에이전트를 가능한 경우 sonnet/haiku 대응 버전으로 교체합니다:

| Opus 에이전트 | 퀵 대체 |
|------------|-------------------|
| `unity-coder` | `unity-coder-lite` |
| `unity-fixer` | `unity-fixer-lite` |
| `unity-reviewer` (이미 sonnet) | 변경 없음 |

예시: `/unity-team --build --quick "add health bar UI"`는 `unity-coder` 대신 `unity-coder-lite`를 사용합니다.

팀 플래그가 지정되지 않으면 기본값은 `--build`입니다.

### 커스텀 팀

```
/unity-team --custom coder,shader-dev "add a dissolve effect to enemy death"
```

에이전트 이름은 `unity-` 접두사를 붙이거나 붙이지 않고 지정할 수 있습니다. 유효한 예: `coder`, `unity-coder`, `shader-dev`, `unity-shader-dev`.

## 실행 흐름

### 1단계: 사전 점검

1. **에이전트 검증** — 요청된 각 에이전트가 `.claude/agents/`에 존재하는지 확인합니다
2. **프로젝트 컨텍스트 읽기** — CLAUDE.md, 최근 git 상태, 어셈블리 구조를 스캔합니다
3. **쓰기 충돌 감지** — 두 에이전트가 동일한 파일을 수정할 가능성이 있으면 사용자에게 경고합니다:
   ```
   WARNING: unity-coder and unity-scene-builder may both modify scene files.
   Proceed anyway? (The reconciliation pass will resolve conflicts.)
   ```
4. **작업 분해** — 각 에이전트를 위한 역할별 브리핑을 생성합니다

### 2단계: 역할 할당

각 에이전트는 공유된 작업 설명과 함께 역할별 지시를 받습니다:

**unity-coder:**
> "다음 기능을 구현하세요. 올바른 네임스페이스, 어셈블리 배치, 아키텍처를 갖춘 C# 스크립트 작성에 집중하세요. 씬 요소는 설정하지 마세요 — 다른 에이전트가 이를 처리하고 있습니다."

**unity-test-runner:**
> "다음 기능에 대한 EditMode 및 PlayMode 테스트를 작성하세요. 구현은 다른 에이전트가 병렬로 작성하고 있습니다 — 구현을 읽지 말고, 작업에 설명된 예상 API를 기반으로 테스트를 작성하세요."

**unity-reviewer:**
> "다음 기능 영역과 관련된 문제에 대해 코드베이스를 검토하세요. 새로운 기능이 건드릴 기존 코드를 확인하세요. 직렬화 안전성, 성능, 아키텍처 관련 사항에 집중하세요."

**unity-scene-builder:**
> "다음 기능을 위한 씬 요소(GameObject, 컴포넌트, 계층 구조, 물리 레이어)를 설정하세요. 스크립트 파일은 다른 에이전트가 작성하고 있습니다 — 씬 구조에만 집중하세요."

**unity-optimizer:**
> "다음 기능과 관련된 영역의 성능을 프로파일링하고 분석하세요. 병목 지점, GC 할당, 렌더링 문제를 식별하세요."

**unity-shader-dev:**
> "다음 기능을 위한 셰이더를 생성하거나 수정하세요. MCP를 통해 머티리얼과 테스트 오브젝트를 설정하세요."

### 3단계: 병렬 실행

Agent 도구를 사용하여 한 메시지 안에서 여러 병렬 호출로 모든 에이전트를 동시에 실행합니다. 각 에이전트는 독립적으로 실행됩니다.

모든 에이전트가 완료될 때까지 기다린 뒤 결과를 수집합니다.

### 4단계: 결과 수집

모든 에이전트의 결과를 통합 보고서로 취합합니다:

```markdown
## Team Results

### unity-coder
- Files created/modified: [list]
- Summary: [agent's summary]

### unity-test-runner
- Tests created: [list]
- Summary: [agent's summary]

### unity-reviewer
- Issues found: [count]
- Summary: [agent's summary]
```

### 5단계: 충돌 감지

에이전트 산출물 간 충돌을 확인합니다:

1. **파일 충돌** — 두 에이전트가 동일한 파일을 수정했나요? 표시하고 두 버전을 모두 제시합니다.
2. **API 불일치** — 테스트가 코더가 만들지 않은 API를 참조하나요? 표시합니다.
3. **명명 불일치** — 서로 다른 에이전트가 동일한 개념에 다른 이름을 사용했나요? 표시합니다.

### 6단계: 조정 (필요한 경우)

충돌이 감지된 경우:

1. 권장 해결책과 함께 충돌 내용을 사용자에게 제시합니다
2. 해결책을 적용합니다(API 표면에는 코더의 구현을 우선하고, 품질 관련 사항에는 리뷰어의 제안을 우선합니다)
3. 최종 일관성 확인을 위해 `unity-verifier` 에이전트를 실행합니다

충돌이 없으면: 바로 최종 보고서로 넘어갑니다.

## 최종 보고서

```markdown
## Team Execution Complete

**Team:** [preset name or custom list]
**Task:** [task description]
**Agents:** [count] ran in parallel

### Created/Modified Files
- [file list with which agent created each]

### Test Coverage
- [test count and pass/fail status]

### Issues Found by Reviewer
- [issue list, noting which were auto-resolved]

### Conflicts Resolved
- [conflict list with resolutions, or "None"]

### Manual Steps Needed
- [any inspector assignments, scene references, etc.]
```

## 유의 사항

- **병렬 에이전트는 독립적으로 작동합니다** — 실행 중 서로의 출력을 볼 수 없습니다
- **테스트 우선 접근** — 병렬로 작성된 테스트는 정확한 구현 API와 일치하지 않을 수 있습니다. 조정 단계에서 이러한 불일치를 잡아냅니다.
- **쓰기 충돌이 발생할 수 있습니다** — 프리셋 팀은 겹침을 최소화하도록 설계되었지만(코더는 스크립트를 쓰고, scene-builder는 씬을 수정하고, 테스터는 테스트 파일을 씁니다), 커스텀 조합은 충돌할 수 있습니다
- **비용** — 3개의 에이전트를 병렬로 실행하면 단일 에이전트 대비 약 3배의 토큰을 사용합니다. 속도 향상이 비용을 정당화하는 중대형 기능에 팀 모드를 사용하세요.
- **MCP 경합** — 여러 에이전트가 동시에 MCP를 사용하면 요청이 대기열에 쌓일 수 있습니다. 이는 정상적으로 처리되지만 MCP 사용이 많은 에이전트의 속도를 늦출 수 있습니다.
