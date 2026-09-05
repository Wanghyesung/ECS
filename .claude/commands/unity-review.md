---
name: unity-review
description: "Unity를 완전히 인지하는 코드 리뷰 — 직렬화 안전성, 성능, 아키텍처, Unity 특유의 함정을 점검합니다."
user-invocable: true
args: scope
---

# /unity-review — Unity 코드 리뷰

Unity 특유의 점검을 포함한 종합적인 코드 리뷰를 수행합니다.

## 에이전트 라우팅

- 기본값: `unity-reviewer` 에이전트 사용 (sonnet — 일반적인 리뷰에 효율적)
- `$ARGUMENTS`에 `--thorough`가 포함된 경우: 더 깊은 아키텍처 분석을 위해 opus 모델을 사용
- 에이전트에 전달하기 전에 인자에서 `--thorough` 플래그를 제거

## 범위

사용자가 범위를 지정한 경우: **$ARGUMENTS**를 리뷰
범위가 지정되지 않은 경우: 최근에 변경된 파일 (`git diff` 또는 `Assets/Scripts/`의 모든 `.cs` 파일)을 리뷰

## 워크플로우

`unity-reviewer` 에이전트를 사용하여 다음을 점검합니다.

### 1. 심각한 문제 (반드시 수정)
- `[FormerlySerializedAs]` 없이 이름이 변경된 `[SerializeField]` 필드
- Unity 오브젝트에 `?.` 또는 `is null` 사용 (반드시 `== null`을 사용해야 함)
- `#if UNITY_EDITOR` 없이 런타임 코드에 포함된 `UnityEditor` 네임스페이스
- MonoBehaviour 클래스 이름이 파일 이름과 일치하지 않음
- `OnDestroy`에서 종료되지 않은 DOTween
- 구독 해제가 짝을 이루지 않는 이벤트 구독
- 처리되지 않은 `async void` 메서드

### 2. 성능 문제 (수정하는 것이 좋음)
- Update/FixedUpdate/LateUpdate 내 GC 할당
- 캐싱되지 않은 `GetComponent`, `Camera.main`, `FindObjectOfType`
- 게임플레이 코드 내 LINQ
- `CompareTag` 대신 사용된 `tag ==`
- `SendMessage` / `BroadcastMessage`
- 캐싱되지 않은 `WaitForSeconds`
- `static readonly`로 캐싱되지 않은 `Animator.StringToHash`

### 3. 아키텍처 제안 (고려 사항)
- 2단계보다 깊은 MonoBehaviour 상속
- 너무 많은 일을 하는 갓 클래스(God class)
- 시스템 간의 강한 결합
- `[SerializeField] private`로 바뀌어야 하는 public 필드
- 누락된 `[RequireComponent]` 속성

### 4. Unity 특유의 경고
- 코루틴 생명주기 문제
- 객체 간 실행 순서 의존성
- 폴백 없는 플랫폼 정의
- FixedUpdate 내 Time.deltaTime

## 출력

구체적인 file:line 참조와 제안된 수정 사항과 함께 심각도별로 그룹화된 결과를 제시합니다.
마지막에 요약을 덧붙입니다: 심각 X건, 성능 Y건, 제안 Z건.
