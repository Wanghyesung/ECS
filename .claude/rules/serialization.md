# 직렬화(Serialization) 규칙

## 중요: FormerlySerializedAs

직렬화된 필드의 이름을 바꿀 때는 반드시 `[FormerlySerializedAs]`를 붙인다 (훅 `cs-lint`가 누락을 경고):

```csharp
// m_fSpeed → m_fMoveSpeed 로 이름 변경 시
[FormerlySerializedAs("m_fSpeed")]
[SerializeField] private float m_fMoveSpeed;
```

**이유:** 없으면 모든 씬/프리팹/ScriptableObject의 값이 **아무 경고 없이 기본값으로 초기화**된다. 이 속성은 영원히 유지 — 절대 제거하지 말 것.

## Unity가 직렬화하는 것

**직렬화됨:** `public` 필드, `[SerializeField] private` 필드. 지원 타입: 기본형, string, Vector/Color/Rect, AnimationCurve, enum, 배열, `List<T>`, `UnityEngine.Object` 참조

**직렬화 안 됨:** 프로퍼티, `static`, `readonly`, `const`, `Dictionary<K,V>`(→ `ISerializationCallbackReceiver`), 인터페이스/추상 타입(→ `[SerializeReference]`)

## 필드 노출

필드 형태는 클래스 종류로 갈린다 (`csharp-unity.md` §4).

```csharp
// 행동 클래스 (MonoBehaviour, BT 노드 같은 로직 SO) — private + 명시적 직렬화
[SerializeField] private float m_fMoveSpeed = 5.0f;

// 데이터 컨테이너 (데이터 SO, [Serializable] class/struct) — public PascalCase, 접두사 없음
public int Damage = 10;
public SOPoolData BossPrefab;
```

- 행동 클래스에 `public float speed;` 처럼 public 필드를 두지 않는다
- `[HideInInspector]` — 숨기지만 직렬화됨 / `[NonSerialized]` — 직렬화 자체를 막음

## Unity Null 체크

```csharp
if (m_refTarget == null)   // 올바름 — 파괴된 오브젝트 감지
    return;

if (m_refTarget is null)   // 틀림 — 감지 못 함
    return;

m_refTarget?.Method();     // 틀림 — 파괴된 오브젝트에서 호출됨
```

## 다형적 직렬화

```csharp
[SerializeReference] private IAbility m_refAbility;   // 없으면 타입 정보 손실
```

## Dictionary 직렬화

```csharp
public sealed class MyData : MonoBehaviour, ISerializationCallbackReceiver
{
    [SerializeField] private List<string> m_listKeys = new();
    [SerializeField] private List<int> m_listValues = new();
    private Dictionary<string, int> m_hashLookup = new();

    public void OnBeforeSerialize()
    {
        m_listKeys.Clear();
        m_listValues.Clear();
        foreach (var tPair in m_hashLookup)
        {
            m_listKeys.Add(tPair.Key);
            m_listValues.Add(tPair.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        m_hashLookup.Clear();
        for (int i = 0; i < m_listKeys.Count; ++i)
            m_hashLookup[m_listKeys[i]] = m_listValues[i];
    }
}
```

## 기타

- 중첩 깊이 7단계에서 직렬화가 조용히 끊긴다
- 프리팹 인스턴스의 필드 변경 = 프리팹 오버라이드. `[FormerlySerializedAs]` 없이 이름을 바꾸면 오버라이드도 전부 사라진다
