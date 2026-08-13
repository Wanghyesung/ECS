# 아키텍처 규칙

## Model-View-System (MVS) 패턴

모든 기능은 엄격한 3계층 분리를 따릅니다:

```
Model  — 순수 C# 클래스. 상태 + 데이터만 포함. Unity API 없음, MonoBehaviour 없음.
View   — MonoBehaviour. Model을 읽고, 비주얼을 렌더링하며, 입력을 전달. 로직 없음.
System — 순수 C# 클래스 (VContainer에 등록됨). Model을 소유하고 변경. 모든 로직 포함.
```

```csharp
// --- Model (순수 C#, 직렬화 가능, Unity 의존성 없음) ---
public sealed class PlayerModel
{
    public ReactiveProperty<int> Health { get; } = new(100);
    public ReactiveProperty<Vector3> Position { get; } = new(Vector3.zero);
    public bool IsDead => Health.Value <= 0;
}

// --- System (순수 C#, VContainer를 통해 주입됨, Model을 소유) ---
public sealed class PlayerSystem : IDisposable
{
    private readonly PlayerModel _model;
    private readonly IPublisher<PlayerDiedMessage> _diedPublisher;

    [Inject]
    public PlayerSystem(PlayerModel model, IPublisher<PlayerDiedMessage> diedPublisher)
    {
        _model = model;
        _diedPublisher = diedPublisher;
    }

    public void TakeDamage(int amount)
    {
        _model.Health.Value = Mathf.Max(0, _model.Health.Value - amount);
        if (_model.IsDead)
        {
            _diedPublisher.Publish(new PlayerDiedMessage());
        }
    }

    public void Dispose() { }
}

// --- View (MonoBehaviour, Model을 관찰함, 로직 없음) ---
public sealed class PlayerView : MonoBehaviour
{
    [SerializeField] private Slider _healthBar;

    private PlayerModel _model;
    private readonly CompositeDisposable _disposables = new();

    [Inject]
    public void Construct(PlayerModel model)
    {
        _model = model;
    }

    private void Start()
    {
        _model.Health
            .Subscribe(hp => _healthBar.value = hp / 100f)
            .AddTo(_disposables);
    }

    private void OnDestroy() => _disposables.Dispose();
}
```

**규칙:**
- Model은 절대 View나 System을 참조하지 않는다
- System은 절대 View를 참조하지 않는다
- View는 `ReactiveProperty<T>.Subscribe()`를 통해 Model을 관찰한다 — Update에서 폴링하지 않는다
- View는 액션을 위해 System을 호출한다 (VContainer를 통해 주입됨)
- 하나의 System은 여러 Model을 소유할 수 있으며, 하나의 View는 하나의 주 Model에 바인딩된다

## 의존성 주입을 위한 VContainer

VContainer는 의존성을 연결하는 **유일한** 방법입니다. 싱글톤도, 정적 접근도, `FindObjectOfType`도 사용하지 않습니다.

### GameContext / 서비스 로케이터 금지 (타협 불가)

여러 의존성을 하나의 주입 가능한 객체로 묶는 `GameContext`, `ServiceLocator`, `Dependencies`, 또는 그 어떤 "만능 컨테이너" 클래스도 만들지 마세요. 이것은 DI의 목적을 무력화시키는 **서비스 로케이터 안티패턴**입니다.

```csharp
// 나쁜 예 — GameContext가 모든 것을 모두에게 노출함
public class GameContext
{
    public PlayerModel Player { get; }
    public ScoreSystem Score { get; }      // SpawnView가 왜 이걸 봐야 하지?
    public SpawnSystem Spawner { get; }    // ScoreView가 왜 이걸 봐야 하지?
    public IAudioService Audio { get; }
}

// 모든 소비자가 모든 의존성에 접근 가능해짐
public sealed class ScoreView : MonoBehaviour
{
    [Inject]
    public void Construct(GameContext ctx)  // 실제 의존성이 숨겨짐
    {
        _score = ctx.Score;  // ctx.Spawner에도 접근 가능함 — 접근 제어가 없음
    }
}
```

**이것이 잘못된 이유:**
- **최소 권한 원칙 위반**: 모든 소비자가 모든 의존성에 접근 가능함
- **실제 의존성을 은폐함**: 생성자/Construct 시그니처가 "ScoreModel이 필요합니다" 대신 "GameContext가 필요합니다"라고 말함
- **테스트 불가능**: 클래스 하나를 테스트하려면 모든 의존성을 가진 전체 GameContext를 구성해야 함
- **바인딩 ≠ 주입**: GameContext 구성이 LifetimeScope 외부의 두 번째 연결 단계가 되어 책임이 중복됨
- **private이어야 할 속성이 노출됨**: GameContext는 특정 소비자만 필요로 하는 의존성에 public 접근을 강제함

```csharp
// 좋은 예 — 각 클래스가 정확히 필요한 것만 선언함
public sealed class ScoreView : MonoBehaviour
{
    private ScoreModel _model;

    [Inject]
    public void Construct(ScoreModel model)  // 필요한 것만 — 그 외에는 아무것도 보이지 않음
    {
        _model = model;
    }
}

public sealed class CombatSystem : IDisposable
{
    private readonly PlayerModel _player;
    private readonly IPublisher<DamageDealtMessage> _pub;

    // 생성자가 정확한 의존성을 선언함 — 자기 설명적이고 테스트 가능함
    public CombatSystem(PlayerModel player, IPublisher<DamageDealtMessage> pub)
    {
        _player = player;
        _pub = pub;
    }
}
```

**규칙:** 모든 클래스는 생성자(System)나 `[Inject] Construct`(View)를 통해 자신의 의존성만 요청합니다. LifetimeScope가 바인딩과 해석이 일어나는 유일한 곳입니다. 중간 컨테이너 객체는 없습니다.

```csharp
public sealed class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Model — 스코프당 싱글톤
        builder.Register<PlayerModel>(Lifetime.Singleton);
        builder.Register<InventoryModel>(Lifetime.Singleton);

        // System — 스코프당 싱글톤
        builder.Register<PlayerSystem>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
        builder.Register<InventorySystem>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

        // Service — 교체 가능하도록 인터페이스 기반으로
        builder.Register<SaveService>(Lifetime.Singleton).As<ISaveService>();

        // View — MonoBehaviour가 아닌 틱을 위해 EntryPoint 사용
        builder.RegisterEntryPoint<GameLoopSystem>();

        // MonoBehaviour View — 씬이나 프리팹에서 찾음
        builder.RegisterComponentInHierarchy<PlayerView>();

        // MessagePipe
        var messagePipeOptions = builder.RegisterMessagePipe();
        builder.RegisterMessageBroker<PlayerDiedMessage>(messagePipeOptions);
        builder.RegisterMessageBroker<ScoreChangedMessage>(messagePipeOptions);
        builder.RegisterMessageBroker<ItemPickedUpMessage>(messagePipeOptions);
    }
}
```

**스코프 계층 구조:**
```
RootLifetimeScope          — 앱 전역 서비스 (오디오, 저장, 설정)
  └─ SceneLifetimeScope    — 씬별 System과 Model
       └─ Child scopes     — 기능별 (예: UI 팝업, 스폰된 엔티티)
```

- 스코프 내에서 공유되는 상태에는 `Lifetime.Singleton`을 사용하세요
- 상태가 없는 서비스나 팩토리에는 `Lifetime.Transient`를 사용하세요
- 주입이 필요한, 동적으로 스폰되는 엔티티에는 자식 스코프를 사용하세요
- 필드에 `[Inject]`를 절대 사용하지 마세요 — System에는 생성자 주입을, MonoBehaviour에는 메서드 주입(`[Inject] public void Construct(...)`)을 사용하세요

## 통신을 위한 MessagePipe

MessagePipe는 **유일한** 메시징 시스템입니다. SO 이벤트 채널도, 정적 EventBus도, 시스템 간 통신을 위한 C# 이벤트도 사용하지 않습니다.

```csharp
// --- 메시지를 readonly struct로 정의 ---
public readonly struct PlayerDiedMessage { }

public readonly struct DamageDealtMessage
{
    public readonly int Amount;
    public readonly Vector3 Position;

    public DamageDealtMessage(int amount, Vector3 position)
    {
        Amount = amount;
        Position = position;
    }
}

// --- 발행 (System → System 또는 System → View) ---
public sealed class CombatSystem : IDisposable
{
    private readonly IPublisher<DamageDealtMessage> _damagePublisher;

    [Inject]
    public CombatSystem(IPublisher<DamageDealtMessage> damagePublisher)
    {
        _damagePublisher = damagePublisher;
    }

    public void DealDamage(int amount, Vector3 position)
    {
        _damagePublisher.Publish(new DamageDealtMessage(amount, position));
    }

    public void Dispose() { }
}

// --- 구독 (System 또는 View에서) ---
public sealed class DamagePopupSystem : IDisposable
{
    private readonly IDisposable _subscription;

    [Inject]
    public DamagePopupSystem(ISubscriber<DamageDealtMessage> damageSubscriber)
    {
        _subscription = damageSubscriber.Subscribe(OnDamageDealt);
    }

    private void OnDamageDealt(DamageDealtMessage message)
    {
        // message.Position 위치에 message.Amount를 표시하는 팝업 스폰
    }

    public void Dispose() => _subscription.Dispose();
}
```

**규칙:**
- 메시지는 `readonly struct`입니다 — 할당량 제로
- 모든 메시지 브로커는 LifetimeScope에 등록하세요
- 구독은 항상 해제하세요 (System은 `IDisposable`을 통해, View는 `CompositeDisposable`을 통해)
- 핸들러가 비동기 작업을 필요로 할 때는 `IAsyncSubscriber<T>`를 UniTask와 함께 사용하세요
- 늦게 구독하는 쪽이 마지막 값을 필요로 할 때는 `IBufferedPublisher<T>` / `IBufferedSubscriber<T>`를 사용하세요

## 비동기를 위한 UniTask

UniTask는 코루틴을 완전히 대체합니다. `StartCoroutine`도, `IEnumerator`도, `yield return`도 사용하지 않습니다.

```csharp
public sealed class WaveSpawnerSystem : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    public async UniTaskVoid StartSpawning()
    {
        for (int waveIndex = 0; waveIndex < 10; waveIndex++)
        {
            await SpawnWave(waveIndex, _cts.Token);
            await UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: _cts.Token);
        }
    }

    private async UniTask SpawnWave(int waveIndex, CancellationToken token)
    {
        int enemyCount = waveIndex * 3;
        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            // 스폰 로직
            await UniTask.Delay(TimeSpan.FromSeconds(0.2f), cancellationToken: token);
        }
    }

    public void Dispose() => _cts.Cancel();
}
```

**규칙:**
- 항상 `CancellationToken`을 전달하세요 — 일반적으로 View에서는 `this.GetCancellationTokenOnDestroy()`, System에서는 `CancellationTokenSource`를 사용
- 대기 가능한 작업에는 `UniTask`를, fire-and-forget에는 `UniTaskVoid`를 사용하세요
- `new WaitForSeconds` 대신 `UniTask.Delay`를 사용하세요
- 병렬 비동기 작업에는 `UniTask.WhenAll`을 사용하세요
- 백그라운드 스레드에서 돌아올 때는 `UniTask.SwitchToMainThread()`를 사용하세요
- `async void`는 사용하지 마세요 — 항상 `async UniTask` 또는 `async UniTaskVoid`를 사용하세요

## 상속보다 조합을 우선

MonoBehaviour는 컴포넌트이지, 베이스 클래스가 아닙니다. 깊은 상속 트리를 만들지 마세요.

MonoBehaviour 상속 최대 깊이: 2 (베이스 + 서브클래스 1개). 그 이상이 필요하면 조합하세요.

View는 가벼워야 합니다 — 로직은 System에, 데이터는 Model에 있어야 합니다.

## 정적 데이터를 위한 ScriptableObject

아이템, 어빌리티, 적 설정, 레벨 데이터 — 이 모두는 ScriptableObject여야 합니다:

```csharp
[CreateAssetMenu(menuName = "Game/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    [SerializeField] private string _displayName;
    [SerializeField] private float _damage;
    [SerializeField] private float _fireRate;
    [SerializeField] private GameObject _prefab;
}
```

ScriptableObject는 **정적/설정 데이터**를 담습니다. 런타임에 변경 가능한 상태는 Model에 있어야 합니다.

## 입력 시스템 아키텍처 (타협 불가)

입력은 **View 계층의 관심사**입니다. 동일한 MVS 패턴을 따릅니다: InputView가 원시 입력을 읽고 System으로 전달합니다. System은 절대 Unity Input을 직접 다루지 않습니다.

### InputView 패턴

```csharp
// InputView — New Input System과 게임 System 사이의 얇은 어댑터
public sealed class InputView : MonoBehaviour
{
    private PlayerControls _controls;
    private PlayerSystem _playerSystem;
    private UISystem _uiSystem;

    private void Awake()
    {
        _controls = new PlayerControls();
    }

    [Inject]
    public void Construct(PlayerSystem playerSystem, UISystem uiSystem)
    {
        _playerSystem = playerSystem;
        _uiSystem = uiSystem;
    }

    private void OnEnable()
    {
        _controls.Player.Enable();
        _controls.Player.Jump.performed += OnJump;
        _controls.Player.Attack.performed += OnAttack;
        _controls.Player.Pause.performed += OnPause;
    }

    private void OnDisable()
    {
        _controls.Player.Jump.performed -= OnJump;
        _controls.Player.Attack.performed -= OnAttack;
        _controls.Player.Pause.performed -= OnPause;
        _controls.Player.Disable();
    }

    private void Update()
    {
        Vector2 move = _controls.Player.Move.ReadValue<Vector2>();
        _playerSystem.SetMoveInput(move);
    }

    private void OnJump(InputAction.CallbackContext ctx) => _playerSystem.Jump();
    private void OnAttack(InputAction.CallbackContext ctx) => _playerSystem.Attack();
    private void OnPause(InputAction.CallbackContext ctx) => _uiSystem.TogglePause();
}
```

### VContainer 등록

```csharp
protected override void Configure(IContainerBuilder builder)
{
    // InputView는 MonoBehaviour입니다 — 씬에서 찾음
    builder.RegisterComponentInHierarchy<InputView>();
}
```

### 규칙
- **InputView가 PlayerControls를 소유합니다** — 다른 어떤 클래스도 `PlayerControls` 인스턴스를 생성하거나 보유하지 않습니다
- **InputView는 View입니다** — 입력을 읽고 System을 호출합니다. 게임 로직은 전혀 없습니다
- **System은 입력에 대해 알지 못합니다** — `SetMoveInput(Vector2)`, `Jump()`, `Attack()` 같은 메서드를 노출합니다. 입력이 어디서 오는지(키보드, 게임패드, AI, 네트워크 리플레이) 절대 알지 못합니다
- **씬당 InputView는 하나** — 중복된 액션 구독을 방지합니다
- **Enable/Disable은 필수입니다** — `OnEnable`은 액션 맵을 활성화하고, `OnDisable`은 이를 비활성화하며 콜백 구독을 해제합니다
- **연속 입력은 Update에서** — `ReadValue<Vector2>()`를 Update에서 읽고 캐싱하세요. 캐싱된 값을 사용해 FixedUpdate에서 물리를 적용하세요
- **불연속 입력은 콜백을 통해** — 버튼 입력은 폴링이 아닌 `performed` 콜백을 사용합니다
- **액션 맵 전환은 InputView에 있습니다** — 메서드 호출을 통해 System이 제어합니다 (예: `SwitchToUI()`, `SwitchToGameplay()`)

### 입력 기반 System 테스트하기

System은 입력에 대해 알지 못하므로, 테스트가 매우 쉽습니다:
```csharp
[Test]
public void SetMoveInput_WithRightVector_UpdatesModelPosition()
{
    var model = new PlayerModel();
    var sut = new PlayerSystem(model);

    sut.SetMoveInput(Vector2.right);
    sut.Tick(1f);

    Assert.That(model.Position.Value.x, Is.GreaterThan(0));
}
```

입력 모킹이 필요 없습니다 — System은 InputAction, PlayerControls, 또는 어떤 Unity Input 타입도 알지 못합니다.

## 의존성 방향

```
View → System → Model
  ↓        ↓
MessagePipe (분리된 통신)
```

- View는 (VContainer 주입을 통해) System과 Model에 의존합니다
- System은 (VContainer 주입을 통해) Model과 다른 System에 의존합니다
- Model은 아무것에도 의존하지 않습니다
- 시스템 간 통신은 직접 참조가 아닌 MessagePipe를 거칩니다
- 어셈블리 정의가 컴파일 타임에 방향을 강제합니다

## 갓 오브젝트(God Object) 금지

```csharp
// 나쁜 예
class GameManager : MonoBehaviour
{
    // 점수, 목숨, 스폰, UI, 오디오, 저장, 입력, 일시정지... 모든 걸 처리함
}

// 좋은 예 — VContainer에 등록된 별도의 System
// PlayerSystem — 체력, 이동
// ScoreSystem — 점수, 콤보
// SpawnSystem — 적 웨이브
// 각각은 주입된 의존성을 가진 순수 C# 클래스
```

## 씬 독립성

각 씬은 LifetimeScope 계층 구조를 통해 독립적으로 로드 가능해야 합니다:
1. `RootLifetimeScope`는 부트스트랩 씬에 존재합니다 (앱 전역 서비스, DontDestroyOnLoad)
2. 각 게임 씬은 Root를 상속하는 자신만의 `SceneLifetimeScope`를 가집니다
3. "이 씬 이전의 씬"에 대한 숨겨진 의존성이 없습니다
4. 씬 로드/언로드는 UniTask를 통해 비동기로 이루어집니다:

```csharp
await SceneManager.LoadSceneAsync("GameScene", LoadSceneMode.Additive).ToUniTask();
```

## 싱글톤 금지

VContainer가 모든 싱글톤 패턴을 대체합니다. 대신 적절한 스코프에서 `Lifetime.Singleton`으로 등록하세요.

- 앱 전역이 필요한가요? `RootLifetimeScope`에 등록하세요
- 씬별로 필요한가요? `SceneLifetimeScope`에 등록하세요
- 기능별로 필요한가요? 자식 스코프를 만드세요
