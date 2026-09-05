---
name: unity-ralph
description: "끈질긴 검증-수정 루프 — 프로젝트가 깨끗해질 때까지 멈추지 않습니다. 설정 가능한 최대 반복 횟수와 정체 감지 기능으로 unity-verifier를 반복 실행합니다."
user-invocable: true
args: options
---

# /unity-ralph — 지속적인 검증-수정 루프

프로젝트가 깨끗해질 때까지 멈추지 않는 끈질긴 검증 루프를 실행합니다: **$ARGUMENTS**

oh-my-claudecode의 지속 실행 모드 이름을 따서 명명되었습니다. `/unity-workflow`가 검증기를 한 번 실행하는 것(내부적으로 최대 3회 반복)과 달리, ralph는 남은 문제가 0개가 되거나 안전 한도에 도달할 때까지 검증기를 반복해서 실행합니다.

## 설정

`$ARGUMENTS`에서 옵션을 파싱합니다.

| 옵션 | 기본값 | 설명 |
|--------|---------|-------------|
| `--max-iterations N` | 10 | 외부 루프의 최대 반복 횟수 (각 반복은 최대 3회의 내부 패스로 검증기를 실행) |
| `--focus <path>` | 전체 | 특정 디렉터리 또는 파일 패턴으로 검증 범위를 제한 |
| `--no-tests` | false | 반복 사이의 테스트 실행을 건너뜀 |

플래그 이후의 모든 내용은 검증기에 전달할 컨텍스트로 취급됩니다 (예: "새 전투 시스템에 초점을 맞춰줘").

## 루프 프로토콜

### 초기화

1. 시작 상태를 기록합니다: `git diff --stat`으로 기준선을 캡처
2. `iteration = 0`, `previous_issues = ""`으로 설정
3. `--focus`가 제공된 경우, 범위 제한 사항을 기록

### 각 반복

```
iteration += 1
echo "Ralph iteration {iteration}/{max}: Starting verification pass..."
```

1. **`unity-verifier` 에이전트 호출** — Agent 도구를 통해 다음과 함께 호출합니다.
   - 초점 범위 (제공된 경우)
   - 이전 반복에서 남은 문제에 대한 컨텍스트
   - 마지막에 구조화된 요약을 보고하라는 지시

2. **검증기의 최종 보고서 수집** — 다음을 확인합니다.
   - "자동 수정 가능한 문제 없음" 또는 이에 상응하는 내용 → **성공, 루프 종료**
   - 자동 수정된 문제 목록 → 기록
   - 남은 문제 목록 → 다음 반복을 위해 캡처

3. **콘솔 확인** — MCP `read_console`을 통해 (사용 가능한 경우) 컴파일 오류 확인

4. **테스트 실행** — MCP `run_tests`를 통해 (사용 가능하고 `--no-tests`가 설정되지 않은 경우)

5. **진행 상황 보고:**
   ```
   Ralph iteration {iteration}/{max}: {fixed_count} issues fixed, {remaining_count} remaining
   ```

### 정체 감지

각 반복 후, 현재 남은 문제를 `previous_issues`와 비교합니다.

- **동일한 문제가 수정 없이 2회 연속 반복에서 지속**되면 → **정체 감지됨**
- 정체 시: 루프를 중단하고 다음을 보고합니다.
  ```
  Ralph stalled after {iteration} iterations. The following issues could not be auto-fixed
  and require human intervention:
  [list of persistent issues]
  ```

`previous_issues`를 현재 남은 문제로 업데이트합니다.

### 종료 조건

다음 중 하나라도 해당되면 루프를 중단합니다.

1. **완료** — 자동 수정 가능한 문제가 더 이상 없고 컴파일이 성공함
2. **최대 반복 도달** — 남은 문제를 보고
3. **정체 감지됨** — 2회 연속 반복에서 동일한 문제 발생
4. **컴파일 손상** — 수정 사항이 검증기의 내부 3-패스 루프에서 해결하지 못한 새 컴파일 오류를 발생시킴

### 실질적인 깊이

각 외부 반복은 최대 3회의 내부 패스를 실행하는 검증기를 호출합니다. 기본 최대 외부 반복 횟수 10회를 기준으로, ralph는 총 **최대 30회의 검증-수정 패스**를 수행할 수 있습니다. 이는 한 문제를 고치면 다음 문제가 드러나는 연쇄적인 문제에 충분한 수준입니다.

## 최종 보고서

```markdown
## Ralph Results

**Status:** Clean | Stalled | Max iterations reached | Compilation broken
**Outer iterations:** {count} of {max}
**Total fixes applied:** {total_fix_count}

### Fixes Applied (by iteration)
#### Iteration 1
- `PlayerController.cs:45` — replaced `?.` with `== null` check
- `EnemySpawner.cs:12` — added `[FormerlySerializedAs("_spawnRate")]`

#### Iteration 2
- `PlayerController.cs:67` — cached GetComponent<Rigidbody>() in Awake

### Remaining Issues (requires human review)
- `GameManager.cs` — class handles 6+ responsibilities, consider splitting
- `UIManager.cs:89` — missing tests for score display logic

### Compilation Status
[PASS / FAIL with error details]

### Test Results
[pass/fail counts or "skipped"]
```

## 규칙

- **아키텍처를 절대 수정하지 않음** — ralph는 안전하고 기계적인 문제만 수정합니다 (검증기의 자동 수정 목록에 있는 항목)
- **검증기의 판단을 존중함** — 검증기가 "사람의 검토 필요"로 표시한 항목은 ralph가 수정을 시도하지 않습니다
- **모든 것을 기록함** — 모든 수정 사항은 반복 회차와 구체적인 문제로 추적 가능해야 합니다
- **안전하게 실패함** — 예상치 못한 일이 발생하면 (MCP 사용 불가, git 상태 손상 등) 즉시 중단하고 보고합니다
- **무한 루프 없음** — 최대 반복 한도와 정체 감지는 타협할 수 없는 안전장치입니다
