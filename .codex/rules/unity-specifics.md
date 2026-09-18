# Unity 특화 규칙

## 에디터 vs 런타임

- `Editor/` 폴더 안의 코드: 에디터 전용, 빌드에서 자동 제외
- `Editor/` 밖에서 `UnityEditor`를 쓰면 **반드시** `#if UNITY_EDITOR`로 가드 — 빠뜨리면 에디터에선 되고 **빌드에서만** 실패 (훅 `guard-editor-runtime`이 차단)

```csharp
#if UNITY_EDITOR
using UnityEditor;
#endif
```

## 플랫폼 정의

항상 폴백을 둘 것 — `#if UNITY_ANDROID` 안의 코드는 다른 플랫폼에서 조용히 사라진다.

```csharp
#if UNITY_ANDROID
    string strPath = Application.persistentDataPath;
#else
    string strPath = Application.dataPath;
#endif
```

## `?.` / `is null` 함정 (가장 흔한 Unity 버그)

Unity는 파괴된 오브젝트를 `== null`로 감지하도록 `==`를 오버라이드한다. `?.`와 `is null`은 C# 참조 비교라 파괴된 오브젝트에서 그대로 메서드를 호출한다.

```csharp
m_refTarget?.TakeDamage(10);                   // 위험 — 파괴된 오브젝트에도 호출됨
if (m_refTarget != null) m_refTarget.TakeDamage(10);  // 안전
```

## 생명주기 순서

```
Awake → OnEnable → Start → FixedUpdate → Update → LateUpdate → OnDisable → OnDestroy
```

- 오브젝트 간 Awake 순서에 의존하지 말 것 — `[DefaultExecutionOrder]` 또는 명시적 Init
- 이벤트 구독 해제는 `OnDisable`에서 (OnDestroy보다 먼저 호출됨)
- 한 번도 활성화되지 않으면 `Start`는 호출되지 않음
- `OnDisable`/`OnDestroy`에서 `Application.isPlaying` 확인 (에디터 도메인 리로드 중 클린업 방지)

## 스레딩

Unity API는 메인 스레드 전용. 백그라운드에서 `Transform`/`GameObject`/`Instantiate`/`Time`/`Physics` 접근 금지. 돌아올 때는 `await UniTask.SwitchToMainThread()`.

## 코루틴 금지 — UniTask

`StartCoroutine`/`IEnumerator`/`yield return` 사용 금지. 규칙과 예제는 `architecture.md`의 "비동기를 위한 UniTask" 참고.

## Time

- `Update`/`LateUpdate`: `Time.deltaTime` — `FixedUpdate`: `Time.fixedDeltaTime`
- 일시정지 무관 로직(UI 애니메이션): `Time.unscaledDeltaTime`

## Transform

- `transform.SetParent(_refParent, false)` — 로컬 트랜스폼 보존은 `worldPositionStays: false`

## 컴포넌트 속성

```csharp
[RequireComponent(typeof(Rigidbody))]   // 자동 추가 + 제거 방지
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
[SelectionBase]
```

## 입력

New Input System 필수. 레거시 `Input.GetKey`/`GetAxis`/`GetMouseButton` 금지 (훅 `cs-lint`가 경고). 구조(`InputManager` 싱글톤 + 생성 C# 클래스)는 `architecture.md`의 "입력 시스템 아키텍처" 참고.

## .meta 파일

- 절대 수동 편집 금지 (훅 `block-meta-edit`이 차단) — 항상 에셋과 함께 커밋
- 에셋 삭제/이동은 Unity(MCP `manage_asset`)를 통해서. 파일만 지우면 GUID 참조가 전부 깨진다
