---
name: event-systems
description: "이벤트 시스템 패턴 — C# 이벤트, UnityEvent, SO 이벤트 채널, static EventBus. 각각을 언제 사용해야 하는지, 무할당(zero-allocation) 패턴, 메모리 누수 방지 방법을 다룹니다."
alwaysApply: true
---

# 이벤트 시스템

이벤트는 시스템을 서로 분리합니다. 발행자는 누가 듣고 있는지 모르고, 구독자는 누가 발행하는지 모릅니다.

## 각 타입을 언제 사용할까

| 타입 | 결합도 | 설정 | 할당 | 가장 적합한 용도 |
|------|----------|--------|------------|----------|
| C# 이벤트 / Action | 코드 전용 | 없음 | struct 사용 시 무할당 | 내부 클래스 간 통신 |
| UnityEvent | 인스펙터로 연결 | 디자이너 | 일부 있음 | 버튼 클릭, 애니메이션 이벤트, 디자이너가 설정 가능한 항목 |
| SO 이벤트 채널 | 에셋 기반 | 디자이너 | 최소 | 시스템 간 통신 |
| Static EventBus | 전역 | 없음 | 상황에 따라 다름 | 진짜 전역적인 이벤트 (드묾) |

## C# 이벤트 (코드에서는 이 방식을 우선 사용)

```csharp
public sealed class HealthSystem : MonoBehaviour
{
    // 이벤트 선언
    public event System.Action<float, float> OnHealthChanged; // 현재 체력, 최대 체력
    public event System.Action OnDied;

    [SerializeField] private float m_fMaxHealth = 100f;
    private float m_fCurrentHealth;

    public void TakeDamage(float _fAmount)
    {
        m_fCurrentHealth = Mathf.Max(0f, m_fCurrentHealth - _fAmount);
        OnHealthChanged?.Invoke(m_fCurrentHealth, m_fMaxHealth);

        if (m_fCurrentHealth <= 0f)
        {
            OnDied?.Invoke();
        }
    }
}

// 구독자
public sealed class HealthBar : MonoBehaviour
{
    [SerializeField] private HealthSystem m_refHealth;

    private void OnEnable()
    {
        m_refHealth.OnHealthChanged += UpdateBar;
        m_refHealth.OnDied += HandleDeath;
    }

    private void OnDisable()
    {
        m_refHealth.OnHealthChanged -= UpdateBar;
        m_refHealth.OnDied -= HandleDeath;
    }

    private void UpdateBar(float _fCurrent, float _fMax)
    {
        // UI 갱신
    }

    private void HandleDeath()
    {
        // 사망 화면 표시
    }
}
```

## 매우 중요: 항상 구독을 해제하세요

**`OnEnable`에서 구독하고 `OnDisable`에서 해제하세요.** 이렇게 하면 다음 상황들을 모두 처리할 수 있습니다:
- 오브젝트 비활성화 (`SetActive(false)` → `OnDisable`)
- 오브젝트 파괴 (`OnDisable` → `OnDestroy` 순으로 호출됨)
- 씬 언로드

```csharp
// 좋은 예 — 구독과 해제가 대칭을 이룸
private void OnEnable() => m_refSource.OnEvent += HandleEvent;
private void OnDisable() => m_refSource.OnEvent -= HandleEvent;

// 나쁜 예 — 오브젝트가 파괴되거나 비활성화되면 메모리 누수 발생
private void Start() => m_refSource.OnEvent += HandleEvent;
// 구독 해제가 없음 → 델리게이트가 참조를 붙들고 있어 오브젝트가 GC될 수 없음
```

## SO 이벤트 채널 (시스템 간 통신)

전체 패턴은 `scriptable-objects` 스킬을 참고하세요. 빠른 참조:

```csharp
// 이벤트 에셋 생성: Assets/Events/OnPlayerDied.asset
// [SerializeField]를 통해 발행자와 구독자에게 동일한 에셋을 연결

// 발행자
[SerializeField] private VoidEventChannel m_SOOnPlayerDied;
m_SOOnPlayerDied.Raise();

// 구독자 (완전히 분리되어 있음 — 발행자에 대해 전혀 모름)
[SerializeField] private VoidEventChannel m_SOOnPlayerDied;
private void OnEnable() => m_SOOnPlayerDied.Subscribe(HandlePlayerDied);
private void OnDisable() => m_SOOnPlayerDied.Unsubscribe(HandlePlayerDied);
```

## UnityEvent (디자이너가 설정 가능)

```csharp
public sealed class InteractableObject : MonoBehaviour
{
    [SerializeField] private UnityEvent m_onInteract;

    public void Interact()
    {
        m_onInteract?.Invoke(); // 디자이너가 인스펙터에서 반응을 연결함
    }
}
```

코드 없이 디자이너가 반응을 설정해야 할 때 사용하세요:
- 버튼 클릭
- 애니메이션 이벤트
- 트리거 존
- 컷신 트리거

**주의:** UnityEvent는 C# 이벤트보다 느리고 디버깅이 어렵습니다. 코드 간 통신에는 C# 이벤트를 사용하세요.

## Static EventBus (전역, 아껴서 사용)

```csharp
public static class GameEvents
{
    public static event System.Action OnGamePaused;
    public static event System.Action OnGameResumed;
    public static event System.Action<int> OnScoreChanged;

    public static void RaisePaused() => OnGamePaused?.Invoke();
    public static void RaiseResumed() => OnGameResumed?.Invoke();
    public static void RaiseScoreChanged(int _iScore) => OnScoreChanged?.Invoke(_iScore);
}
```

**경고:** static 이벤트는 절대 가비지 컬렉션되지 않습니다. 구독자는 반드시 구독을 해제해야 합니다. 모든 시스템이 필요로 하는 진짜 전역 이벤트에만 사용하세요.

## 무할당 패턴 (핫 패스)

매 프레임 발생하는 이벤트의 경우(드물지만 존재합니다):

```csharp
// 힙 할당을 피하기 위해 ref struct 사용
public readonly ref struct DamageEvent
{
    public readonly float Amount;
    public readonly Vector3 Position;
    public readonly GameObject Source;

    public DamageEvent(float _fAmount, Vector3 _vPosition, GameObject _refSource)
    {
        Amount = _fAmount;
        Position = _vPosition;
        Source = _refSource;
    }
}

// 참고: ref struct는 Action<T> 델리게이트에 저장할 수 없음
// 무할당을 유지하려면 커스텀 델리게이트나 직접 메서드 호출을 사용하세요
```

## 흔한 실수

1. **구독만 하고 해제하지 않음** → 메모리 누수, 파괴된 오브젝트에서 이벤트가 발생함
2. **Awake에서 구독하고 OnDestroy에서 해제** → 오브젝트가 비활성화된 상태에서도 이벤트가 발생함
3. **동기적이고 같은 프레임 안에서 끝나는 로직에 이벤트를 사용** → 직접 메서드 호출이 더 단순함
4. **너무 많은 이벤트 채널** → 발행자와 구독자가 항상 함께 존재한다면 직접 참조를 사용하세요
