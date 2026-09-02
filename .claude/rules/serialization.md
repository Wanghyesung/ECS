# 직렬화(Serialization) 규칙

## 중요: FormerlySerializedAs

직렬화된 필드의 이름을 변경할 때는 반드시 `[FormerlySerializedAs]`를 추가해야 합니다:

```csharp
// _speed를 _moveSpeed로 이름을 변경하는 경우:
[FormerlySerializedAs("_speed")]
[SerializeField] private float _moveSpeed;
```

**이유:** 이것이 없으면 Unity는 기존 직렬화 데이터를 새 필드 이름에 매핑할 수 없습니다. 모든 씬, 프리팹, ScriptableObject에 설정된 값이 아무런 경고 없이 조용히 기본값으로 초기화됩니다. 아티스트/디자이너의 수 시간에 걸친 작업이 흔적도 없이 사라집니다.

`[FormerlySerializedAs]` 속성은 영원히 유지되어야 합니다. 절대 제거하지 마세요.

## Unity가 직렬화하는 것

**직렬화됨:**
- `public` 필드 (`[NonSerialized]`가 없는 경우)
- `[SerializeField] private` 필드
- 지원되는 타입: 기본형, string, Vector2/3/4, Color, Rect, AnimationCurve, enum, 배열, List<T>, UnityEngine.Object 참조

**직렬화되지 않음:**
- 프로퍼티 (`[SerializeField]`가 붙어 있어도)
- `static` 필드
- `readonly` 필드
- `const` 필드
- `Dictionary<K,V>` (`ISerializationCallbackReceiver` 사용)
- 인터페이스/추상 타입 (`[SerializeReference]` 사용)

## 필드 노출

```csharp
// 좋은 예 — private이면서 명시적으로 직렬화됨
[SerializeField] private float _health = 100f;

// 나쁜 예 — public은 인스펙터와 코드 양쪽에 노출됨
public float health = 100f;  // (참고: public이 필요하다면 lowerCamelCase를 사용하세요)

// 자동 프로퍼티의 경우 (C# 7.3 이상):
[field: SerializeField] public float Health { get; private set; }
```

- `[HideInInspector]` — 인스펙터에서 숨기지만 여전히 직렬화됩니다
- `[NonSerialized]` — 직렬화를 완전히 막습니다 (public 필드의 캐싱/계산된 데이터에 사용)

## Unity Null 체크

```csharp
// 올바른 예 — Unity는 파괴된 오브젝트를 감지하기 위해 ==를 오버라이드함
if (_target == null) return;

// 잘못된 예 — Unity의 파괴된 오브젝트 감지를 우회함
if (_target is null) return;      // C# null 체크, 파괴 여부를 감지하지 못함
if (_target?.Method() != null)    // ?.는 Unity의 ==를 우회하며, 파괴된 오브젝트에서 메서드를 호출함
```

Unity 오브젝트는 "파괴"될 수 있지만 가비지 컬렉션되지는 않을 수 있습니다. `== null`은 파괴된 오브젝트에 대해 true를 반환합니다. `?.`와 `is null`은 C# 참조 동등성을 사용하는데, 이는 false를 반환하여 파괴된 오브젝트에 대한 호출로 이어질 수 있습니다.

## 다형적 직렬화

```csharp
// 인터페이스/추상 필드의 경우:
[SerializeReference] private IAbility _ability;
```

`[SerializeReference]`가 없으면 Unity는 값으로 직렬화하며 타입 정보를 잃어버립니다.

## 커스텀 타입

```csharp
// 콜백을 통한 딕셔너리 직렬화:
public class MyData : MonoBehaviour, ISerializationCallbackReceiver
{
    [SerializeField] private List<string> _keys = new();
    [SerializeField] private List<int> _values = new();

    private Dictionary<string, int> _lookup = new();

    public void OnBeforeSerialize()
    {
        _keys.Clear();
        _values.Clear();
        foreach (KeyValuePair<string, int> pair in _lookup)
        {
            _keys.Add(pair.Key);
            _values.Add(pair.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        _lookup.Clear();
        for (int i = 0; i < _keys.Count; i++)
        {
            _lookup[_keys[i]] = _values[i];
        }
    }
}
```

## 깊이 제한

Unity 직렬화는 7단계의 중첩에서 멈춥니다. 깊이 중첩된 데이터 구조는 조용히 잘려나갑니다.

## 프리팹 오버라이드

프리팹 인스턴스에서 직렬화된 필드를 변경하면 프리팹 오버라이드가 생성됩니다. `[FormerlySerializedAs]`는 이름 변경 중에도 이 오버라이드를 보존합니다. 이것이 없으면 모든 오버라이드가 사라집니다.
