---
name: unity-interview
description: "소크라테스식 인터뷰 흐름 — 요구사항을 탐색하고, 엣지 케이스를 식별하며, 범위를 명확히 하고, 코딩을 시작하기 전에 구조화된 기능 브리프를 출력합니다."
user-invocable: true
args: topic
---

# /unity-interview — 심층 요구사항 인터뷰

다음 주제에 대해 철저한 다단계 요구사항 인터뷰를 진행합니다: **$ARGUMENTS**

이 커맨드는 코드가 한 줄도 작성되기 전에 포괄적인 기능 브리프를 만들어냅니다. `/unity-workflow`의 간단한 clarify 단계보다 의도적으로 훨씬 더 철저하게 설계되었습니다 — 요구사항을 정확히 파악하는 것이 중요한 규모가 크거나 모호한 기능에 이 커맨드를 사용하십시오.

## 1단계: 범위 탐색

사용자에게 경계를 정의해달라고 요청합니다:

1. **이 기능은 무엇을 하는가?** — 핵심 동작, 주요 사용 사례
2. **이 기능은 무엇을 하지 않는가?** — 명시적 제외 항목, 범위 밖 항목
3. **사용자는 누구인가?** — 플레이어 대상인가, 디자이너 대상인가, 개발자용 도구인가?
4. **무엇이 이를 트리거하는가?** — 플레이어 입력, 게임 이벤트, 타이머, 외부 신호?
5. **기대되는 출력은 무엇인가?** — 시각적 변화, 데이터 변형, 이벤트 발행, 오디오?

진행하기 전에 범위를 요약합니다. 사용자에게 확인하거나 조정해달라고 요청합니다.

## 2단계: 기술 요구사항

프로젝트 컨텍스트를 자동으로 수집하고 목표가 명확한 질문을 합니다:

1. `CLAUDE.md`를 **읽어서** 프로젝트 설정(Unity 버전, 렌더 파이프라인, 대상 플랫폼, 패키지)을 파악합니다
2. `Packages/manifest.json`을 **읽어서** 사용 가능한 패키지를 식별합니다
3. 기존 어셈블리 정의를 **스캔하여** 프로젝트 구조를 파악합니다
4. 다음에 대해 질문합니다:
   - **성능 예산** — 목표 FPS는? 메모리 한계는? 최대 드로우콜 수는?
   - **플랫폼 제약** — 모바일 발열 스로틀링? WebGL 크기 제한?
   - **Unity 서브시스템** — 물리, UI, 애니메이션, 오디오, 네트워킹, Addressables?
   - **데이터 영속성** — 저장이 필요한 상태가 있는가? 어떤 형식으로?
   - **멀티플레이어** — 네트워크 관련 요소가 있는가? 권한(authority) 모델은?

## 3단계: 엣지 케이스 식별

1-2단계에서 식별된 각 주요 컴포넌트에 대해 체계적으로 탐색합니다:

1. **오류 상태** — 문제가 발생하면 어떻게 되는가? (null 참조, 에셋 누락, 네트워크 실패)
2. **경계 조건** — 최솟값/최댓값, 빈 컬렉션, 0초 타이머
3. **플랫폼 차이** — iOS와 Android에서 다르게 동작하는가? 에디터와 빌드에서는?
4. **경합 조건(race condition)** — 씬 전환, 비동기 작업, 파괴(destroy) 타이밍
5. **부하 상태에서의 성능** — 예상 엔티티/아이템/파티클 수의 100배가 되면 어떻게 되는가?
6. **되돌리기/리셋** — 플레이어가 이 동작을 되돌릴 수 있는가? 씬을 다시 로드하면 어떻게 되는가?

발견한 엣지 케이스를 제시하고 묻습니다: "제가 놓친 것이 있나요? 명시적으로 제외하고 싶은 것이 있나요?"

## 4단계: 통합 지점 매핑

기능이 접촉하는 기존 시스템을 모두 식별합니다:

1. 읽거나, 쓰거나, 구독될 **시스템 목록**을 작성합니다
2. 각 시스템에 대해 다음을 명확히 합니다:
   - **데이터 흐름 방향** — 새 기능이 읽는가, 쓰는가, 둘 다인가?
   - **소유권** — Model을 누가 소유하는가? 어떤 System이 이를 변경하는가?
   - **메시지 의존성** — 어떤 MessagePipe 메시지를 발행/구독하는가?
3. **새로운 의존성 식별** — 필요한 새 패키지, 서비스, 에셋이 있는가?
4. **어셈블리 배치** — 새 코드는 어떤 어셈블리 정의에 위치해야 하는가?

시스템 간 데이터 흐름을 보여주는 (텍스트 기반) 통합 다이어그램을 제시합니다.

## 5단계: 인수 조건 (Acceptance Criteria)

테스트 가능한 인수 조건의 번호가 매겨진 목록을 작성합니다:

```
## Acceptance Criteria

1. [ ] [Specific, testable condition]
2. [ ] [Another condition]
...
```

인수 조건 작성 규칙:
- 각 조건은 **독립적으로 테스트 가능**해야 합니다
- 모호한 표현이 아니라 구체적인 값을 사용합니다 ("체력이 감소한다"가 아니라 "체력이 10만큼 감소한다")
- 최소 하나의 **부정 테스트**(발생하지 않아야 하는 것)를 포함합니다
- 관련이 있다면 최소 하나의 **성능 조건**을 포함합니다 ("적 50마리를 스폰해도 30FPS 아래로 떨어지지 않는다")

사용자에게 조건을 확인, 추가, 삭제해달라고 요청합니다.

## 출력: 구조화된 기능 브리프

모든 단계가 완료된 후, 포괄적인 문서를 생성합니다:

```markdown
## Feature Brief: [Title]

### Scope
- **Does:** [bullet list from Phase 1]
- **Does NOT:** [explicit exclusions]
- **Trigger:** [what initiates the feature]
- **Output:** [what the feature produces]

### Technical Requirements
- **Unity:** [version] | **Pipeline:** [URP/HDRP/Built-in] | **Platform:** [targets]
- **Subsystems:** [physics, UI, animation, etc.]
- **Performance budget:** [FPS, memory, draw calls]
- **Data persistence:** [yes/no, format]

### Edge Cases
| Case | Expected Behavior |
|------|-------------------|
| [edge case] | [what should happen] |

### Integration Points
| System | Direction | Messages |
|--------|-----------|----------|
| [system] | read/write/both | [MessagePipe messages] |

### Assembly Placement
- New scripts go in: `[assembly name]`
- New tests go in: `[test assembly name]`

### Acceptance Criteria
1. [ ] [criterion]
2. [ ] [criterion]
...

### Estimated Complexity
[simple / moderate / complex] — [brief justification]

### Recommended Approach
[1-3 sentences on how to implement, which agents to use]
```

## 규칙

- **단계를 건너뛰지 마십시오** — 각 단계는 이전 단계를 기반으로 합니다
- **가정하지 말고 질문하십시오** — 무언가 불명확하면 추측하지 말고 질문하십시오
- **각 단계 후 요약하십시오** — 사용자가 일찍 방향을 수정할 수 있게 합니다
- **코드는 다루지 마십시오** — 이 커맨드는 브리프를 생성할 뿐 코드를 생성하지 않습니다. 구현에는 `/unity-workflow` 또는 `/unity-feature`를 사용하십시오.
- **사용자의 시간을 존중하십시오** — 사용자가 상세하게 답변했다면 이미 명확한 내용을 다시 묻지 마십시오
