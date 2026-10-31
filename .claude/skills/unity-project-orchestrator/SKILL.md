---
name: unity-project-orchestrator
description: "플레이어·몬스터·아이템·UI처럼 여러 Unity 모듈이 함께 필요한 프로젝트 단위 기능을 계약 우선으로 분해하고, 명시적 파일 소유권 아래 unity-module-worker 서브에이전트를 최대 3개 병렬 실행한 뒤 메인 세션이 MCP 배치·통합·검증한다. '여러 시스템/모듈을 한꺼번에', '프로젝트 단위로', '병렬로 나눠서' 같은 요청에 발동. 단일 기능이나 국소 버그에는 사용하지 않는다(→ workflow_view / unity-workflow / unity-fix)."
---

# Unity Project Orchestrator

프로젝트 단위 요청을 공용 계약, 독립 모듈, Unity 통합 단계로 분리한다. 병렬성보다 충돌 없는 소유권과 검증 가능한 인계를 우선한다.

CLAUDE.md의 "서브에이전트는 사용자가 시키거나 커맨드가 명시할 때만" 규칙에서 **이 스킬이 곧 명시하는 커맨드**다. 서브에이전트 사용은 이 스킬의 3단계(병렬 구현)와 4단계 독립 리뷰(`unity-reviewer`)에 한정한다.

## 적용 판단

다음 중 두 가지 이상이면 적용한다.

- 서로 다른 런타임 책임이 세 개 이상이다.
- 플레이어/몬스터/아이템/UI처럼 독립 파일 집합으로 나눌 수 있다.
- 씬·프리팹·ScriptableObject 연결이 여러 모듈에 걸친다.
- 순차 구현보다 독립 구현의 비중이 높다.

단일 기능, 국소 리팩터링, 버그 수정에는 기존 `workflow_view`, `/unity-workflow`, `/unity-fix`를 사용한다.

## 필수 문서

작업 시작 시 `.claude/docs/orchestration/<project-id>/`를 만들고 다음 템플릿을 복사해 채운다.

- 프로젝트 계약: `.claude/docs/orchestration/templates/project.md` → `<project-id>/project.md`
- 모듈 작업: `.claude/docs/orchestration/templates/work-item.md` → `<project-id>/work-items/<work-item-id>.md`
- 작업자 인계: `.claude/docs/orchestration/templates/handoff.md` → 작업자가 `<project-id>/ready-for-integration/<work-item-id>.md`에 작성

```
.claude/docs/orchestration/<project-id>/
├── project.md                      # 메인 전용
├── work-items/<id>.md              # 메인이 작성, 작업자는 status 줄만 갱신
├── ready-for-integration/<id>.md   # 작업자가 작성 (handoff_path)
└── done/<id>.md                    # 메인이 통합 완료 후 이동
```

형식과 상태 규칙이 필요하면 [작업 패키지 규격](references/work-package-schema.md)을 읽는다. 다른 하네스로 옮기는 요청에는 `.claude/docs/orchestration/harness-transfer.md`도 읽는다.

문서 구조는 작업자를 띄우기 **전**과 통합 **전**에 검증한다:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/unity-project-orchestrator/scripts/Test-OrchestrationPackage.ps1 -PackagePath .claude/docs/orchestration/<project-id>
```

`owned_paths` 중복, 필수 섹션 누락, 인계서 누락을 잡는다. 통과하지 못하면 작업자를 띄우지 않는다.

## 실행 순서

### 1. 조사와 계약 고정

1. CLAUDE.md 채팅 규칙대로 ① 비슷한 기존 코드 ② 핫 루프 영향 ③ SO/이벤트 확장 지점을 한 줄씩 먼저 보고한다.
2. 메인 세션이 요구사항과 수용 기준, 모듈 의존 그래프(`project.md`의 Mermaid)를 작성한다.
3. 공용 인터페이스·이벤트 payload(R3 `Subject<T>` 타입)·ID/enum·직렬화 데이터·어셈블리 방향을 먼저 확정하고 **계약 파일을 메인이 직접 작성**한다. 작업자는 계약을 소비만 한다.
4. 인터페이스가 실제로 둘 이상의 구현/소비 경계를 만들지 않으면 만들지 않는다.
5. R3가 필요하면 `Packages/manifest.json`에 `com.cysharp.r3`가 있는지 확인한다. 미설치면 `r3` 스킬의 설치 안내를 먼저 제시하고 R3 코드를 작성하지 않는다.

공용 계약, 씬, 프리팹, SO 에셋, `ProjectSettings/**`, `Packages/**`, 공용 asmdef는 메인 세션이 단독 소유한다.

이 단계의 산출물(계약 + 의존 그래프 + 분해안)을 사용자에게 보여주고 **승인 후** 2단계로 넘어간다. `architecture.md` 규칙대로 다이어그램 먼저.

### 2. 작업 분해

각 작업 항목(`work-items/<id>.md`)에 다음을 반드시 지정한다.

- 한 문장 책임과 제외 범위
- 다른 작업과 겹치지 않는 `owned_paths`
- 읽기만 가능한 `shared_read_only_paths`
- 소비/구현할 공용 계약
- 직렬화 클래스·필드 및 필요한 SO
- 필요한 GameObject·컴포넌트와 MCP 배치 요구
- 단독 검증 방법과 통합 수용 기준
- 고유한 `handoff_path`

쓰기 경로가 겹치면 병렬 작업으로 만들지 않는다. 선행 작업이 필요한 항목은 `depends_on`에 적고 의존성이 해결된 뒤 다음 배치에서 실행한다.

### 3. 병렬 구현

- `Agent` 도구로 `subagent_type: "unity-module-worker"`를 **한 메시지에 최대 세 개** 호출한다. 한 메시지 안의 독립 호출은 병렬로 실행된다. 네 개 이상은 다음 배치로 미룬다.
- `isolation: "worktree"`는 쓰지 않는다 — Unity `Library/`와 MCP는 프로젝트 경로 하나에 묶여 있어 워크트리마다 Unity를 따로 띄울 수 없다. 충돌 방지는 워크트리가 아니라 `owned_paths`로 한다.
- 각 호출의 `prompt`에는 작업 항목 경로와 프로젝트 계약 경로를 그대로 넣는다. 작업자는 이 대화를 볼 수 없으므로 요구사항을 요약해 다시 쓰지 말고 **문서 경로를 읽게** 한다:

```
Agent(
  subagent_type: "unity-module-worker",
  description: "<work-item-id> 모듈 구현",
  prompt: "작업 항목: .claude/docs/orchestration/<project-id>/work-items/<work-item-id>.md
           프로젝트 계약: .claude/docs/orchestration/<project-id>/project.md
           두 문서를 먼저 읽고 owned_paths 안에서만 구현한 뒤 handoff_path에 인계서를 작성하라."
)
```

- 작업자에게 다른 작업자의 파일을 수정하거나 공용 계약을 재설계할 권한을 주지 않는다. 계약 변경이 필요하다는 보고가 오면 해당 작업만 `blocked`로 두고 1단계로 돌아간다.
- 메인 세션은 작업자가 도는 동안 공용 파일을 변경하지 않는다. 불가피하면 관련 작업자의 완료를 기다린 뒤 변경한다.
- 작업자는 백그라운드로 돌고 완료 시 알림이 온다. **모든 작업자의 알림이 도착할 때까지** 통합을 시작하지 않는다. 아직 오지 않은 결과를 예측하거나 지어내지 않는다.
- 작업자의 최종 보고는 사용자에게 보이지 않으므로 메인이 요약해 전달한다.
- 각 작업자가 끝나면 `git diff --name-only`로 실제 변경 파일이 그 작업의 `owned_paths` 안인지 확인한다. 경계 밖 변경이 있으면 해당 작업을 `blocked`로 바꾸고 사용자에게 보고한다 — 조용히 되돌리지 않는다.
- 같은 작업자에게 후속 수정을 시킬 때는 새 `Agent`를 띄우지 말고 `SendMessage`(ToolSearch로 로드)로 그 작업자의 컨텍스트를 이어서 쓴다.

### 4. 통합과 Unity 배치

메인 세션이 다음 순서로 **직렬** 수행한다.

1. 계약 준수와 중복 구현 확인 (`Test-OrchestrationPackage.ps1` 재실행 포함)
2. Unity 컴파일 확인 (`mcp__UnityMCP__read_console` — `unity-mcp-patterns` 스킬)
3. 인계서의 MCP 작업을 의존 순서대로 실행 (`manage_gameobject` / `manage_components` / `manage_scriptable_object` / `manage_prefabs`, 여러 개면 `batch_execute`)
4. 씬 인스턴스·프리팹·SO 참조 재조회 (`find_gameobjects` 등 — 프리팹만 보고 판단하지 않는다)
5. EditMode 후 PlayMode 테스트 (`run_tests` → `get_test_job`)
6. 통합 시나리오 수동 또는 자동 검증
7. 독립 리뷰가 필요한 위험도면 `unity-reviewer` 에이전트 실행 (읽기 전용)

씬·프리팹·`.meta` 파일을 텍스트로 직접 편집하지 않는다(훅이 차단). MCP 연결이 불가능하면 상태를 `blocked` 또는 `ready-for-integration`으로 유지하고 필요한 수동 단계를 보고한다.

### 5. 완료 판정

작업자가 코드를 끝냈다는 이유만으로 `done`으로 바꾸지 않는다. 다음이 모두 충족되어야 메인이 인계 문서를 `done/`으로 이동하고 작업 항목 `status`를 `done`으로 바꾼다.

- 실제 변경 파일이 소유 범위 안이다.
- 컴파일 오류가 없다.
- 관련 테스트가 통과했다.
- 필요한 씬·프리팹·SO 연결이 적용되고 재조회로 확인됐다.
- 통합 수용 기준이 통과했다.

일부 모듈이 실패하면 성공 모듈을 되돌리지 않는다. 실패 작업만 수정하거나 동일 작업자에게 후속 요청하고, 공용 계약 변경이 필요하면 영향을 받는 작업을 중단한 뒤 새 배치로 재계획한다.

커밋은 하지 않는다(훅이 차단). 완료 후 `commit` 스킬로 사용자에게 커밋 명령을 넘긴다.

## 최종 보고

- 완료 모듈과 보류 모듈
- 공용 계약과 직렬화 타입
- MCP로 생성/연결한 오브젝트
- 컴파일·테스트·통합 검증 결과
- 다른 프로젝트로 가져갈 하네스 파일 목록 (`harness-transfer.md`)
