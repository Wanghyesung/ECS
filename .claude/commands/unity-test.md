---
name: unity-test
description: "누락된 테스트를 작성하고 MCP를 통해 실행합니다. 테스트되지 않은 코드를 식별하고, EditMode/PlayMode 테스트를 생성하며, run_tests를 통해 실행하고 결과를 보고합니다."
user-invocable: true
args: scope
---

# /unity-test — 테스트 작성 및 실행

프로젝트에 대한 테스트를 작성하고 MCP를 통해 실행합니다.

## 범위

사용자가 범위를 지정한 경우: **$ARGUMENTS**를 테스트합니다
범위가 지정되지 않은 경우: 테스트되지 않은 것 중 가장 중요한 코드 경로를 식별합니다.

## 작업 흐름

`unity-test-runner` 에이전트를 사용하여 다음을 수행합니다:

### 1단계: 테스트 커버리지 평가

1. 기존 테스트 어셈블리(테스트 참조가 있는 `.asmdef` 파일)를 찾습니다
2. 테스트 어셈블리가 존재하지 않으면 생성합니다:
   - `ProjectName.Tests.Editor` — EditMode 테스트 (빠름, 씬 불필요)
   - `ProjectName.Tests.Runtime` — PlayMode 테스트 (전체 라이프사이클)
3. 테스트가 없는 공개 API를 가진 스크립트를 식별합니다
4. 우선순위: 게임플레이 로직 > 시스템 > 유틸리티

### 2단계: 테스트 작성

테스트되지 않은 각 클래스/메서드에 대해:
- 순수 로직이면(MonoBehaviour 라이프사이클이 필요 없음) **EditMode 테스트**
- MonoBehaviour, 물리, 또는 씬 상태가 관련되면 **PlayMode 테스트**
- 명명 규칙: `MethodName_Condition_ExpectedResult`
- Arrange-Act-Assert 패턴
- TearDown에서 GameObject를 정리

### 3단계: 테스트 실행

```
run_tests → execute all tests (or specific fixture if scoped)
read_console → get test results
```

### 4단계: 보고

결과를 제시합니다:
- 총계: 통과 X개, 실패 Y개, 스킵 Z개
- 실패 항목에 대해서는: 테스트 이름, 예상값 대 실제값, 스택 트레이스, 제안하는 수정 방법
- 새로 생성된 테스트: 파일 경로와 함께 목록으로
- 커버리지 공백: 아직 테스트가 필요한 부분

## 테스트 우선순위

1. **게임 상태 로직** — 체력, 데미지, 점수, 인벤토리
2. **입력 처리** — 이동 계산, 능력 활성화
3. **데이터 시스템** — 저장/불러오기, 직렬화, 설정
4. **엣지 케이스** — 체력 0, 빈 인벤토리, null 참조
