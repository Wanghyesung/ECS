# 작업 패키지 규격

## 상태

- `planned`: 계약과 소유권은 확정됐지만 구현 전
- `in-progress`: 작업자가 구현 중
- `ready-for-integration`: 작업자 검증과 인계 완료, 메인 통합 전
- `blocked`: 소유권·계약·외부 상태 때문에 진행 불가
- `done`: 메인이 Unity 배치와 통합 검증까지 완료
- `example`: 실행되지 않는 구조 검증용 예제

서브에이전트는 `ready-for-integration`까지만 설정한다.

## 소유권

`owned_paths`는 파일 또는 디렉터리 glob이며 작업 항목끼리 겹치면 안 된다. 다음은 항상 메인 전용이다.

- 공용 계약과 공용 데이터 타입
- `.unity`, `.prefab`, `.meta`
- `ProjectSettings/**`, `Packages/**`
- 여러 모듈이 참조하는 `asmdef`

## 인계 필수 정보

- 실제 변경 파일
- 구현/소비한 계약
- `[Serializable]` 타입과 `[SerializeField]` 필드
- 생성할 SO와 초기 데이터
- GameObject 계층, 추가 컴포넌트, 연결할 필드
- MCP 작업 순서와 대상 경로
- 실행한 테스트와 결과
- 미해결 위험과 통합 시 주의점

## 직렬화 경계

- 인터페이스는 런타임 상호작용에 사용한다.
- Inspector에는 구체적인 `MonoBehaviour`/`ScriptableObject` 참조를 사용한다.
- 일반 인터페이스 필드를 `[SerializeField]`로 선언하지 않는다.
- `[SerializeReference]`는 다형 직렬화가 실제 요구사항일 때만 사용한다.
- 런타임 상태를 ScriptableObject에 저장하지 않는다.
