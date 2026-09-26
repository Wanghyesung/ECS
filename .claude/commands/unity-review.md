---
name: unity-review
description: "Unity를 완전히 인지하는 코드 리뷰 — 직렬화 안전성, 성능, 프로젝트 코드 스타일, 아키텍처, Unity 특유의 함정을 점검합니다."
user-invocable: true
args: scope
---

# /unity-review — Unity 코드 리뷰

Unity 특유의 점검과 프로젝트 코드 스타일(`rules/csharp-unity.md`) 점검을 포함한 종합적인 코드 리뷰를 수행합니다.

## 에이전트 라우팅

- 기본값: `unity-reviewer` 에이전트 사용 (sonnet — 일반적인 리뷰에 효율적)
- `$ARGUMENTS`에 `--thorough`가 포함된 경우: 더 깊은 아키텍처 분석을 위해 opus 모델을 사용
- 에이전트에 전달하기 전에 인자에서 `--thorough` 플래그를 제거

## 범위

사용자가 범위를 지정한 경우: **$ARGUMENTS**를 리뷰
범위가 지정되지 않은 경우: 커밋되지 않은 `.cs` 변경 (`git diff --name-only HEAD -- '*.cs'` + `git ls-files --others --exclude-standard -- '*.cs'`). 변경이 없으면 그렇다고 말하고 끝낸다 — 폴더 전체를 훑지 않는다.

## 워크플로우

`unity-reviewer` 에이전트의 체크리스트로 다음을 점검합니다.

### 1. 심각한 문제 (반드시 수정)
- `[FormerlySerializedAs]` 없이 이름이 변경된 `[SerializeField]` 필드
- Unity 오브젝트에 `?.` 또는 `is null` 사용 (반드시 `== null`)
- `#if UNITY_EDITOR` 없이 런타임 코드에 포함된 `UnityEditor` 네임스페이스
- MonoBehaviour/ScriptableObject 클래스 이름이 파일 이름과 다름
- `OnDestroy`에서 종료되지 않은 DOTween
- 해제 짝이 없는 R3 구독 (`AddTo`/`Dispose`/`DisposableBag` 없음)
- `async void`, CancellationToken 누락

### 2. 성능 문제 (수정하는 것이 좋음)
- Update/FixedUpdate/LateUpdate 내 GC 할당, 캐싱되지 않은 `GetComponent`/`Camera.main`/`FindObjectOfType`
- 게임플레이 코드 내 LINQ, `tag ==`, `SendMessage`
- 코루틴·`WaitForSeconds` (UniTask 로)
- 풀 대상의 `Instantiate`/`Destroy` 직접 호출
- 할당하는 PhysX 쿼리 — 판정 대상이 1500개를 넘으면 자체 Collider (collider-system)
- `static readonly`로 캐싱되지 않은 `Animator.StringToHash`/`Shader.PropertyToID`

### 3. 스타일 위반 (rules/csharp-unity.md)
- 접두사(`m_`/`_`/타입), Transform `Tr`, `_tToken`
- 같은 줄 if 본문, 한 줄 블록, `!` 대신 `== false`, `++i`
- 싱글톤 형태, 새 파일 헤더(`기능 :`)·`sealed`·CreateAssetMenu 형식
- 데이터 컨테이너의 public PascalCase 필드는 위반이 아니다

### 4. 아키텍처 제안 (고려 사항)
- 2단계보다 깊은 MonoBehaviour 상속, 갓 클래스
- 발행자가 구독자를 알 필요가 없는데 직접 참조 (매니저 싱글톤 호출은 허용)
- 행동 클래스의 public 필드
- SO 에 런타임 상태 쓰기, leaf BT 노드의 상태 필드

### 5. Unity 특유의 경고
- 객체 간 실행 순서 의존성 (`Awake`에서 다른 싱글톤 접근 등)
- 폴백 없는 플랫폼 정의
- FixedUpdate 내 `Time.deltaTime`

## 출력

구체적인 file:line 참조와 제안된 수정 사항과 함께 심각도별로 그룹화된 결과를 제시합니다.
마지막에 요약을 덧붙입니다: 심각 X건, 성능 Y건, 스타일 S건, 제안 Z건.
