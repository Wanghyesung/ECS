# C# / Unity 규칙

- 저장소의 기존 네이밍과 가시성 규칙을 우선한다. 새 프로젝트에 공통 규칙이 없으면 `private` 필드와 명확한 이름을 사용한다.
- `MonoBehaviour` 참조는 가능한 한 `Awake`에서 캐싱하고 `Update`·`FixedUpdate`에서 반복 검색하지 않는다.
- Unity 오브젝트의 파괴 여부는 `== null`로 검사한다. `?.`와 `is null`은 Unity의 파괴 감지를 따르지 않는다.
- 직렬화 필드 이름을 변경할 때 `FormerlySerializedAs`를 사용한다.
- `UnityEditor` 참조는 `Editor` 폴더에 두거나 `UNITY_EDITOR` 가드로 감싼다.
- 비동기, 입력, 풀링 구현은 설치된 패키지와 프로젝트 규칙을 확인한 뒤 선택한다.
