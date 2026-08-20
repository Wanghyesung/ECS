---
name: unity-tdd-pipeline
description: "Plan 모드 계획 → 셀프 리뷰 → TDD(Red-Green-Refactor) → 씬 통합까지, 서브에이전트를 새로 띄우지 않고 지금 이 세션에서 순차적으로 직접 수행하는 경량 TDD 파이프라인."
user-invocable: true
args: feature_description
---

# /unity-tdd-pipeline — 단일 컨텍스트 TDD 파이프라인

다음 기능을 4단계 순차 파이프라인으로 개발합니다: **$ARGUMENTS**

**이 커맨드는 어떤 단계에서도 `Agent` 도구를 호출하지 않습니다.** 계획, 스텁 작성, 테스트 작성, 구현, 리팩토링, 씬 통합까지 전부 지금 이 세션(이 컨텍스트)이 직접 수행합니다. 
서브에이전트를 새로 띄우면 매번 프로젝트 구조와 지금까지의 계획을 처음부터 다시 파악해야 해서 느려지고, 그 비용이 이 파이프라인이 무거워지는 가장 큰 원인이었습니다. 
`/unity-team`(병렬 다중 에이전트)과는 반대 극단에 있는 커맨드입니다 — 병렬 에이전트도, 순차 에이전트 체인도 아니고 **에이전트 자체가 없습니다.**

## 전체 흐름

```
1. 계획 (Plan 모드) — 직접 분석 + 완료 기준(=테스트 케이스) 정의 (+ 선택적 셀프 리뷰)
   ↓ ExitPlanMode (사용자 승인)
2. RED — 스텁 작성 → 실패하는 테스트 작성 → run_tests로 실패 확인
3. GREEN → REFACTOR — 최소 구현 → run_tests 통과 확인 → 셀프 리뷰 자동수정
4. INTEGRATE — 씬 배선 보완 → 성능 점검(실제 배치된 에셋 대상) → 최종 확인 → 리포트
```

병렬로 묶을 수 있는 건 **단계 자체가 아니라 단계 안의 독립적인 도구 호출들**입니다 — 예: Stage 1의 여러 파일 Read/Grep/Glob, Stage 4의 여러 GameObject 조회, 마지막 `read_console`+`run_tests`. 서로 결과를 필요로 하지 않는 호출은 한 메시지에 같이 실행하세요. 반면 1→2→3→4 사이, 그리고 2 안의 스텁→테스트→확인 순서는 절대 앞뒤를 바꾸거나 건너뛰지 마세요 — 다음 단계의 입력이 이전 단계의 산출물이라 순서 자체가 TDD의 핵심입니다.

## 상태 파일

`.claude/state/session.json`에 두 종류의 상태를 기록합니다:

- **`tdd.phase`** — 실제로 도구 레벨에서 강제됩니다. `.claude/hooks/guard-tdd-red.sh`가 이 값을 읽어서, `"red-pending"`인 동안 테스트가 아닌 새 `.cs` 파일 `Write`를 차단합니다. 서브에이전트가 없는 이 설계에서는 "실패 테스트 확인 없이 바로 구현부터 쓰는" 실수를 막는 유일한 안전장치이므로, 아래 단계에서 지시하는 시점에 **반드시** 전환하세요.
- **`workflow_phase`** — 정보 기록용입니다(`/unity-sessions`에서 진행 단계를 보여주는 데 씀). 어떤 훅도 이 값으로 막지 않으니 순서를 어겨도 안전하지만, 최신 상태로 유지해 두면 세션이 끊겼다 이어질 때 도움이 됩니다.

```bash
STATE_DIR=".claude/state"
STATE_FILE="$STATE_DIR/session.json"
mkdir -p "$STATE_DIR"
[ -f "$STATE_FILE" ] || echo '{}' > "$STATE_FILE"
jq '<필드 갱신 표현식>' "$STATE_FILE" > "$STATE_FILE.tmp" && mv "$STATE_FILE.tmp" "$STATE_FILE"
```

## Stage 0 — 인자 파싱

`$ARGUMENTS`에서 기능 설명과 `--no-critic` 플래그를 분리합니다. `--no-critic`이 있으면 Stage 1의 셀프 리뷰 패스를 건너뜁니다.

## Stage 1 — 계획 (Plan 모드)

1. `EnterPlanMode` 호출
2. 분석합니다:
   - 생성/수정할 스크립트 목록과 어셈블리 배치
   - 필요한 씬 변경(GameObject, 컴포넌트, 물리 레이어)
   - **TDD 대상이 될 공개 API와 완료 기준**을 명시적으로 정의 — Stage 2에서 이것이 곧 테스트 케이스가 됩니다
   - 관련 기존 코드 스캔이 필요하면 Read/Grep/Glob을 직접 호출하되, 서로 독립적인 조회는 한 메시지에 병렬로 묶으세요. 필요한 전문 스킬(`object-pooling`, `event-systems` 등)은 `Skill` 도구로 직접 로드하세요.
3. 위 내용을 계획 파일에 작성
4. `--no-critic`이 아니면: `.claude/rules/architecture.md`와 `.claude/rules/performance.md`의 체크리스트로 계획을 스스로 재검토합니다 
— SO 데이터 오염, God Object, 싱글톤 남용, Update 내 GC 할당, 드로우콜/풀링 리스크 등. 도구 호출 없이 순수하게 계획서를 다시 읽으며 점검하면 되므로 비용이 거의 들지 않습니다. 발견한 문제는 계획에 바로 반영합니다.
5. 상태 기록(정보용):
   ```bash
   jq --arg desc "$FEATURE_DESC" '.workflow_phase = "Plan" | .plan.description = $desc' "$STATE_FILE" > "$STATE_FILE.tmp" && mv "$STATE_FILE.tmp" "$STATE_FILE"
   ```
6. `ExitPlanMode` 호출 — 계획 파일에 최종본 반영 후 사용자 승인 대기

## Stage 2 — RED

사용자가 계획을 승인한 뒤에만 진행합니다.

1. **스텁 작성** — 계획에 따라 클래스/메서드 시그니처만 직접 `Write`합니다. 메서드 본문은 최소 스텁(값 반환 메서드는 `default`/`throw new NotImplementedException()`, void 메서드는 빈 본문). `.claude/rules/`의 네이밍·캡슐화·asmdef 배치 규칙을 따르세요. (아직 `tdd.phase`가 설정되지 않았으므로 훅에 막히지 않습니다.)
2. `read_console`으로 컴파일 오류 확인 — 스텁 시그니처 오류는 지금 바로 고칩니다(TDD 대상이 아닌 단순 문법 문제이므로).
3. 상태 전환:
   ```bash
   jq '.tdd.phase = "red-pending"' "$STATE_FILE" > "$STATE_FILE.tmp" && mv "$STATE_FILE.tmp" "$STATE_FILE"
   ```
   (이 시점부터 `guard-tdd-red.sh`가 테스트가 아닌 신규 `.cs` 파일 생성을 차단합니다 — 지금부터는 테스트만 쓸 수 있습니다.)
4. **실패 테스트 작성** — Stage 1에서 정의한 완료 기준을 검증하는 테스트를 스텁의 공개 API를 대상으로 직접 `Write`합니다. 스텁이 아직 구현되지 않았으므로 반드시 실패해야 합니다.
5. `run_tests` + `get_test_job`으로 **실패를 직접 확인**합니다(컴파일 자체가 안 되는 것도 RED로 인정하되, 의도한 이유인지 확인). 예상과 다르게 통과한다면 테스트가 완료 기준을 제대로 검증하지 못하는 것이므로 테스트를 다시 작성합니다.
6. 상태 전환(이제 구현 파일 작성 가능):
   ```bash
   jq '.tdd.phase = "red-confirmed"' "$STATE_FILE" > "$STATE_FILE.tmp" && mv "$STATE_FILE.tmp" "$STATE_FILE"
   ```

## Stage 3 — GREEN → REFACTOR

### 3-1. GREEN

1. 실패하는 테스트를 통과시키는 **최소** 구현을 스텁에 직접 채웁니다. 테스트가 요구하지 않는 기능을 추가하지 마세요.
2. `run_tests`로 **통과 확인**. 실패하면 직접 원인을 고쳐 재시도합니다(최대 2회, 넘으면 사용자에게 보고하고 판단을 구합니다).
3. 상태 전환:
   ```bash
   jq '.tdd.phase = "green-confirmed"' "$STATE_FILE" > "$STATE_FILE.tmp" && mv "$STATE_FILE.tmp" "$STATE_FILE"
   ```

### 3-2. REFACTOR

`unity-verifier`가 쓰던 것과 같은 체크리스트를 직접 적용하는 자동수정 루프입니다(최대 2회 반복):

1. 이번 단계에서 변경된 파일을 대상으로 점검:
   - 이름이 변경된 `[SerializeField]` 필드에 `[FormerlySerializedAs]` 누락
   - Unity 오브젝트에 대한 `?.`/`is null` → `== null`
   - `tag == "..."` → `CompareTag(...)`
   - Update/FixedUpdate/LateUpdate 안의 `GetComponent<T>()`/`Camera.main`/`FindObjectOfType` → Awake 캐싱
   - 런타임 코드의 `UnityEditor` 사용에 `#if UNITY_EDITOR` 가드 누락
   - Update 안의 `new WaitForSeconds()` → 필드 캐싱
   - `async void` → `async UniTaskVoid`
2. 자동 수정 가능한 것만 직접 `Edit`으로 고치고, 무엇을 왜 고쳤는지 기록합니다. 아키텍처 판단이 필요한 것(갓 클래스, 결합도, 디자인 패턴 선택)은 고치지 말고 최종 리포트에 "사람 검토 필요"로 남깁니다.
3. 수정했다면 `run_tests`로 재확인 — 방금 수정 때문에 테스트가 깨지면 **그 수정만 되돌리고** 사람 검토 항목으로 표시합니다.
4. 더 이상 자동 수정할 게 없거나 2회 반복에 도달하면 종료합니다.

## Stage 4 — INTEGRATE

1. **계획 대 실제 대조** — `find_gameobjects`, `manage_components` 등으로 현재 씬 상태를 조회해(서로 다른 오브젝트 대상 조회는 병렬로) 계획에서 요구한 GameObject/컴포넌트/인스펙터 참조가 다 붙어 있는지 확인합니다.
2. **누락분 보완** — 계획에는 있었지만 씬에 아직 반영되지 않은 배선(새 필드의 인스펙터 참조, Stage 3에서 새로 생긴 public 메서드의 UnityEvent 바인딩 등)을 `batch_execute`로 한 번에 채웁니다. 계획에 없던 새 씬 구조를 임의로 추가하지 마세요.
3. **성능 점검** — 이번 단계에서 실제로 배치된 프리팹/컴포넌트/파티클/ScriptableObject를 대상으로 `.claude/rules/performance.md` 체크리스트를 점검합니다. **Stage 1의 셀프 리뷰와 역할이 다릅니다** — Stage 1은 코드가 없는 시점의 계획 텍스트만 보고 예측하는 것이고, 여기는 씬에 실제로 존재하는 에셋을 `manage_asset`/`manage_components`/`manage_scriptable_object`/`get_info`/`get_hierarchy` 등으로 직접 조회해서 확인합니다. 예측만으로는 못 잡는 것들(그림자 캐스팅 On/Off, 파티클 시스템의 루핑·Max Particles·머티리얼 블렌드 모드, 오브젝트 풀 PreLoad가 실제 발사 빈도 대비 충분한지, 메시 폴리곤 수 등)이 대상입니다.
   - `manage_components`/`manage_scriptable_object`로 바로 고칠 수 있는 것(그림자 토글, PreLoad 수치 조정 등)은 즉시 고치고 무엇을 왜 고쳤는지 기록합니다.
   - 셰이더 교체, 메시 단순화, 파티클 서브시스템 재설계처럼 더 깊은 에디터 작업이 필요한 것은 고치지 말고 최종 리포트의 "Manual steps needed"에 구체적인 단계(어느 메뉴, 어떤 설정)와 함께 남깁니다 — `performance.md`의 "개발자 액션 아이템" 컨벤션과 동일합니다.
4. **최종 확인** — `read_console`과 `run_tests`를 병렬로 호출해 씬 변경/성능 수정 후 새 에러나 회귀가 없는지 확인합니다.
5. 상태 완료 기록:
   ```bash
   jq '.workflow_phase = "Complete" | del(.tdd)' "$STATE_FILE" > "$STATE_FILE.tmp" && mv "$STATE_FILE.tmp" "$STATE_FILE"
   ```
6. **최종 리포트**를 사용자에게 제시합니다:
   ```markdown
   ## Pipeline Complete

   ### What was built
   - [구현된 기능 요약]

   ### Files created/modified
   - [파일 경로 + 한줄 설명]

   ### Scene wiring completed
   - [이번 단계에서 보완한 GameObject/컴포넌트/참조 목록, 없으면 "누락 없음"]

   ### Performance check
   - [실제 에셋 대상으로 발견/수정한 항목, 없으면 "이슈 없음"]

   ### Verification results
   - 컴파일: PASS/FAIL
   - 테스트: N passed, M failed
   - Refactor 단계에서 남겨둔 사람 검토 필요 항목: [목록, 없으면 "없음"]

   ### Manual steps needed
   - [인스펙터/에디터에서 사람이 직접 해야 할 것 — 성능 점검에서 못 고친 항목 포함, 없으면 "없음"]

   ### How to test
   - [단계별 플레이 테스트 방법]
   ```

## 설계 원칙

- **에이전트 스폰 없음** — 이 세션이 계획부터 통합까지 전부 직접 수행합니다. 매번 새 컨텍스트를 만드는 비용(프로젝트/계획 재파악)이 이 파이프라인이 무거워졌던 핵심 원인이었기 때문입니다.
- **순차만 허용** — Stage 1→2→3→4, 그리고 Stage 2 안의 스텁→red-pending 전환→테스트→확인 순서를 절대 건너뛰지 마세요. TDD의 전제 자체가 이 순서입니다.
- **독립적인 도구 호출은 병렬 허용** — 같은 단계 안에서 서로 결과를 필요로 하지 않는 Read/Grep/MCP 조회는 한 메시지에 같이 호출해 시간을 아끼세요. 이건 "새 컨텍스트를 만드는 것"이 아니라 같은 컨텍스트 안에서 도구를 동시에 쓰는 것이라 안전합니다.
- **훅이 Red-before-Green을 실제로 강제합니다** — `guard-tdd-red.sh`는 프롬프트 지시를 어겨도 도구 레벨에서 막습니다. Stage 2의 `tdd.phase` 전환을 정확한 시점에 실행하세요 — 스텁 작성 *전에* `red-pending`으로 바꾸면 스텁 자체가 막히고, 실패 확인 *후에* `red-confirmed`로 바꾸지 않으면 Stage 3의 구현이 막힙니다.
- **게이트는 Plan 승인 하나뿐** — `ExitPlanMode`가 유일한 사용자 확인 지점입니다. Stage 3 GREEN에서 2회 재시도로도 실패하면 자동 진행을 멈추고 사람에게 판단을 구합니다.
