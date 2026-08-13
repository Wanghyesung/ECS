---
name: serialization-safety
description: "Unity 직렬화 규칙 — 이름 변경 시 FormerlySerializedAs, SerializeField와 public의 차이, 다형성을 위한 SerializeReference, Unity null 체크(`?.`가 아닌 `== null`). 매우 중요: 조용한 데이터 손실을 방지합니다."
alwaysApply: true
---

# 직렬화 안전성

이것은 가장 중요한 스킬입니다. 직렬화 실수는 **조용한 데이터 손실**을 유발합니다 — 모든 씬, 프리팹, ScriptableObject에 설정된 값이 아무런 경고 없이 기본값으로 초기화됩니다.

## 규칙 1: 이름을 변경할 때는 반드시 FormerlySerializedAs

```csharp
// 변경 전: 필드 이름이 m_fSpeed
[SerializeField] private float m_fSpeed = 5f;

// 변경 후: m_fMoveSpeed로 이름 변경 — FormerlySerializedAs를 반드시 추가해야 함
[FormerlySerializedAs("m_fSpeed")]
[SerializeField] private float m_fMoveSpeed = 5f;
```

**이유:** Unity는 필드를 이름으로 직렬화합니다. 이름을 바꾸면 이름 → 값 매핑이 깨집니다. 이 필드를 설정해둔 모든 씬, 프리팹, SO가 조용히 값을 잃어버립니다. `[FormerlySerializedAs]`는 Unity에게 "이 필드는 원래 X라는 이름이었다"라고 알려주는 역할을 합니다.

이 속성은 **영원히** 남겨둬야 합니다. 절대 제거하지 마세요.

## 규칙 2: Unity Null 체크

```csharp
// 올바른 예 — Unity는 파괴된 오브젝트를 감지하기 위해 ==를 오버라이드함
if (m_refTarget == null) return;
if (m_refTarget != null) m_refTarget.TakeDamage(10);

// 잘못된 예 — Unity의 파괴된 오브젝트 감지를 우회함
if (m_refTarget is null) return;        // C# null 체크, 파괴 여부를 감지하지 못함
m_refTarget?.TakeDamage(10);            // ?.는 Unity의 ==를 우회하며, 파괴된 오브젝트에서도 메서드를 호출함
m_refTarget ??= FindNewTarget();        // ??=는 Unity null이 아닌 C# null을 사용함
```

**이유:** Unity 오브젝트는 (C++ 쪽 메모리는 해제되어) "파괴"될 수 있지만, 아직 가비지 컬렉션되지 않아 C# 참조는 그대로 남아있을 수 있습니다. Unity는 파괴된 오브젝트에 대해 `true`를 반환하도록 `==`를 오버라이드합니다. C#의 패턴 매칭(`is null`, `?.`, `??`)은 참조 동일성을 사용하므로 `false`를 반환합니다 — 그 결과 파괴된 오브젝트에서 메서드를 호출하게 되어 크래시나 예측할 수 없는 동작으로 이어집니다.

## 규칙 3: Unity가 직렬화하는 대상

**직렬화됨:**
- `public` 필드 (`[NonSerialized]`가 없는 경우)
- `[SerializeField] private/protected` 필드
- 타입: `int`, `float`, `bool`, `string`, `Vector2/3/4`, `Color`, `Rect`, `Quaternion`, `AnimationCurve`, `Gradient`, enum, `UnityEngine.Object` 하위 클래스, 배열, `List<T>`, `[Serializable]` 구조체/클래스

**직렬화되지 않음:**
- 프로퍼티(getter/setter) — `[SerializeField]`가 붙어 있어도 직렬화되지 않음
- `static` 필드
- `readonly` 필드
- `const` 필드
- `Dictionary<K,V>` — `ISerializationCallbackReceiver`를 사용하세요
- 인터페이스 / 추상 타입 — `[SerializeReference]`를 사용하세요
- 델리게이트 / 이벤트

## 규칙 4: public보다 SerializeField Private을 우선

```csharp
// 좋은 예 — 통제된 노출
[SerializeField] private float m_fHealth = 100f;
public float Health => m_fHealth; // 읽기 전용 접근

// 나쁜 예 — 누구나 수정할 수 있고 API가 지저분해짐
public float health = 100f;
```

## 규칙 5: 다형성을 위한 SerializeReference

```csharp
// SerializeReference가 없으면: Unity는 기반 타입으로 직렬화해 파생 데이터를 잃어버림
[SerializeField] private IAbility m_refAbility; // 오류: 인터페이스는 직렬화되지 않음

// SerializeReference가 있으면: 다형적 직렬화가 가능
[SerializeReference] private IAbility m_refAbility; // 정상 동작: 구체 타입이 저장됨
```

## 규칙 6: 캐싱된 데이터에는 NonSerialized

```csharp
public class Enemy : MonoBehaviour
{
    [SerializeField] private float m_fMaxHealth = 100f;

    [NonSerialized] public float m_fCurrentHealth; // 런타임 전용, 저장되지 않음
    
    private Transform m_refCachedTransform; // private이면 기본적으로 직렬화되지 않음 (좋은 예)
}
```

## 규칙 7: 딕셔너리를 위한 ISerializationCallbackReceiver

```csharp
public class DataStore : MonoBehaviour, ISerializationCallbackReceiver
{
    // Unity가 직렬화하는 리스트
    [SerializeField] private List<string> m_listKeys = new();
    [SerializeField] private List<float> m_listValues = new();

    // 런타임 딕셔너리 (직접 직렬화되지 않음)
    private Dictionary<string, float> m_hashData = new();

    public void OnBeforeSerialize()
    {
        m_listKeys.Clear();
        m_listValues.Clear();
        foreach (KeyValuePair<string, float> pair in m_hashData)
        {
            m_listKeys.Add(pair.Key);
            m_listValues.Add(pair.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        m_hashData = new Dictionary<string, float>();
        for (int i = 0; i < m_listKeys.Count; i++)
        {
            m_hashData[m_listKeys[i]] = m_listValues[i];
        }
    }
}
```

## 규칙 8: 직렬화 깊이 제한

Unity는 **7단계** 중첩에서 직렬화를 멈춥니다. 깊이 중첩된 데이터 구조는 조용히 잘려나갑니다. 깊은 데이터가 필요하다면 평탄화하거나 `[SerializeReference]`를 사용하세요.

## 규칙 9: HideInInspector와 NonSerialized의 차이

- `[HideInInspector]` — 인스펙터에서는 숨기지만 **여전히 직렬화됩니다** (데이터가 저장됨)
- `[NonSerialized]` — 직렬화를 완전히 막습니다 (데이터가 저장되지 않고, 플레이할 때마다 초기화됨)

## 규칙 10: 자동 프로퍼티 직렬화

```csharp
// C# 7.3+에서 직렬화 가능한 자동 프로퍼티 문법
[field: SerializeField] public float Speed { get; private set; }

// 참고: FormerlySerializedAs는 백킹 필드 이름을 사용함:
[field: FormerlySerializedAs("<Speed>k__BackingField")]
[field: SerializeField] public float MoveSpeed { get; private set; }
```
