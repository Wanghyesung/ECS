---
name: scriptable-objects
description: "ScriptableObject 아키텍처 패턴 — 이벤트 채널, 변수 참조, 런타임 세트, 팩토리 패턴, 데이터 컨테이너. 데이터 기반 Unity 아키텍처의 근간입니다."
alwaysApply: true
---

# ScriptableObject 아키텍처

ScriptableObject(SO)는 Unity에서 가장 강력한 아키텍처 도구입니다. 씬 밖에 존재하는 에셋 기반 데이터 컨테이너로, 데이터 기반 설계와 느슨한 결합, 디자이너 친화적인 워크플로우를 가능하게 합니다.

## 패턴 1: 데이터 컨테이너

가장 단순하고 흔한 사용법 — 게임 데이터를 에셋으로 정의합니다.

```csharp
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Game/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string m_strDisplayName;
    [SerializeField] private Sprite m_refIcon;
    [TextArea]
    [SerializeField] private string m_strDescription;

    [Header("Stats")]
    [SerializeField] private float m_fDamage = 10f;
    [SerializeField] private float m_fFireRate = 0.5f;
    [SerializeField] private int m_iAmmoCapacity = 30;
    [SerializeField] private GameObject m_refPrefab;

    public string DisplayName => m_strDisplayName;
    public Sprite Icon => m_refIcon;
    public float Damage => m_fDamage;
    public float FireRate => m_fFireRate;
    public int AmmoCapacity => m_iAmmoCapacity;
    public GameObject Prefab => m_refPrefab;
}
```

**용도:** 아이템, 어빌리티, 적 설정, 레벨 데이터, 오디오 이벤트, UI 테마 등.

## 패턴 2: 이벤트 채널

시스템 간 결합을 아예 없애는 통신 방식입니다. 직접 참조가 필요 없습니다.

```csharp
[CreateAssetMenu(fileName = "NewVoidEvent", menuName = "Events/Void Event")]
public sealed class VoidEventChannel : ScriptableObject
{
    private System.Action m_onRaised;

    public void Raise()
    {
        m_onRaised?.Invoke();
    }

    public void Subscribe(System.Action _listener)
    {
        m_onRaised += _listener;
    }

    public void Unsubscribe(System.Action _listener)
    {
        m_onRaised -= _listener;
    }
}

// 값을 전달하는 이벤트를 위한 제네릭 버전
[CreateAssetMenu(fileName = "NewIntEvent", menuName = "Events/Int Event")]
public sealed class IntEventChannel : ScriptableObject
{
    private System.Action<int> m_onRaised;

    public void Raise(int _iValue) => m_onRaised?.Invoke(_iValue);
    public void Subscribe(System.Action<int> _listener) => m_onRaised += _listener;
    public void Unsubscribe(System.Action<int> _listener) => m_onRaised -= _listener;
}
```

**사용 예:**
```csharp
// 발행자 (예: ScoreSystem)
[SerializeField] private IntEventChannel m_SOOnScoreChanged;
m_SOOnScoreChanged.Raise(iNewScore);

// 구독자 (예: ScoreUI) — 인스펙터에서 발행자와 동일한 SO 에셋을 연결
[SerializeField] private IntEventChannel m_SOOnScoreChanged;
private void OnEnable() => m_SOOnScoreChanged.Subscribe(UpdateDisplay);
private void OnDisable() => m_SOOnScoreChanged.Unsubscribe(UpdateDisplay);
```

## 패턴 3: 변수 참조

인스펙터에서 조정 가능하며, 여러 곳에서 공유하거나 인스턴스별로 오버라이드할 수 있는 값입니다.

```csharp
[System.Serializable]
public sealed class FloatReference
{
    [SerializeField] private bool m_bUseConstant = true;
    [SerializeField] private float m_fConstantValue;
    [SerializeField] private FloatVariable m_SOVariable;

    public float Value => m_bUseConstant ? m_fConstantValue : m_SOVariable.Value;
}

[CreateAssetMenu(fileName = "NewFloatVar", menuName = "Variables/Float")]
public sealed class FloatVariable : ScriptableObject
{
    [SerializeField] private float m_fValue;
    public float Value { get => m_fValue; set => m_fValue = value; }
}
```

**용도:** 플레이어 체력, 이동 속도, 중력 등 — 디자이너가 조정하고 여러 시스템이 읽는 값.

## 패턴 4: 런타임 세트

FindObjectsOfType 없이 특정 타입의 모든 활성 인스턴스를 추적합니다.

```csharp
[CreateAssetMenu(fileName = "NewRuntimeSet", menuName = "Sets/Transform Set")]
public sealed class TransformRuntimeSet : ScriptableObject
{
    private readonly List<Transform> m_listItems = new();

    public IReadOnlyList<Transform> Items => m_listItems;
    public int Count => m_listItems.Count;

    public void Add(Transform _refItem)
    {
        if (!m_listItems.Contains(_refItem))
        {
            m_listItems.Add(_refItem);
        }
    }

    public void Remove(Transform _refItem)
    {
        m_listItems.Remove(_refItem);
    }
}

// 사용 예: 적이 스스로를 등록한다
public sealed class Enemy : MonoBehaviour
{
    [SerializeField] private TransformRuntimeSet m_SOEnemySet;

    private void OnEnable() => m_SOEnemySet.Add(transform);
    private void OnDisable() => m_SOEnemySet.Remove(transform);
}
```

## 패턴 5: 팩토리 설정

```csharp
[CreateAssetMenu(fileName = "NewSpawnConfig", menuName = "Game/Spawn Config")]
public sealed class SpawnConfiguration : ScriptableObject
{
    [SerializeField] private GameObject m_refPrefab;
    [SerializeField] private int m_iPoolSize = 10;
    [SerializeField] private float m_fSpawnRate = 2f;
    [SerializeField] private float m_fSpawnRadius = 15f;

    public GameObject Prefab => m_refPrefab;
    public int PoolSize => m_iPoolSize;
    public float SpawnRate => m_fSpawnRate;
    public float SpawnRadius => m_fSpawnRadius;
}
```

## 안티패턴

1. **플레이 세션 간 초기화되지 않는 가변 상태** — SO는 에디터 안에서 플레이 세션을 넘어 값이 유지됩니다. 런타임에 SO에 값을 쓰면 플레이를 멈춰도 그 값이 그대로 남습니다. `OnEnable`이나 `OnDisable`에서 초기화하거나, 별도의 런타임 복사본을 사용하세요.

2. **싱글톤처럼 사용되는 SO** — `Resources.Load`로 SO를 전역에서 접근하지 마세요. `[SerializeField]` 참조나 DI를 사용하세요.

3. **SO에 지나치게 많은 로직** — SO는 데이터와 이벤트를 위한 것입니다. 복잡한 로직은 시스템/서비스 쪽에 두세요.

4. **순환 SO 참조** — SO A가 SO B를 참조하고, SO B가 다시 SO A를 참조하는 경우입니다. 직렬화 과정에서 무한 루프를 일으킬 수 있습니다.
