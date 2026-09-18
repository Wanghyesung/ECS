# 아키텍처 규칙

> ⚠️ **프로젝트 결정 사항**
> - **VContainer(DI) 사용 안 함.** 의존성은 `[SerializeField]` 직접 참조 또는 절제된 싱글톤.
> - 시스템 간 알림은 **R3** (2026-09-15 결정: 프로젝트의 `event Action` 전부 R3로 전환). 1회성 알림 = `Subject<T>` → `Observable<T>` 노출, 계속 변하는 값 = `ReactiveProperty<T>`. `UnityEvent`는 인스펙터 바인딩용으로만.
> - 연속적으로 변하는 값의 Model→View 바인딩은 **R3**(`ReactiveProperty<T>`) — [[r3]] 스킬 참고. UniRx 아님.
> - 비동기는 **UniTask** 전용. 코루틴 금지.
> - 입력은 **New Input System + 생성 C# 클래스**를 `InputManager` 싱글톤이 소유 (아래 참고).
> - **외부 라이브러리 API 확인은 Context7** (아래 표). 추측으로 API 를 쓰지 말 것.

## 외부 라이브러리 문서 — Context7

UniTask/R3/DOTween 의 시그니처·오버로드·예제가 기억에 확실치 않으면 **코드를 쓰기 전에** 아래 URL 을 `WebFetch` 로 읽는다. `<API>` 에 메서드/클래스명을 넣어 필요한 부분만 가져온다 (예: `?topic=WaitUntilValueChanged`).

| 라이브러리 | URL |
|---|---|
| UniTask | `https://context7.com/cysharp/unitask/llms.txt?topic=<API>&tokens=3000` |
| R3 | `https://context7.com/cysharp/r3/llms.txt?topic=<API>&tokens=3000` |
| DOTween | `https://context7.com/demigiant/dotween/llms.txt?topic=<API>&tokens=3000` |

## Mermaid 다이어그램 작성 규칙

기능 설계는 **Mermaid `sequenceDiagram` 하나**로 실제 클래스·메서드의 실행 순서를 보여준다. 클래스·상태·흐름 다이어그램은 사용자가 별도로 요청할 때만 추가한다. 계획 승인이 필요한 작업에서는 코드와 시퀀스를 함께 제시한다.

## 조건식 가독성

한 `if`의 조건은 **최대 3개**다(`&&`/`||`를 합쳐 최대 2개). 자세한 기준은 [csharp-unity.md](csharp-unity.md)의 제어 흐름 규칙을 따른다. 복잡한 전제는 책임에 맞는 가드로 나누고, 개수 제한을 피하려고 긴 조건을 프로퍼티나 메서드 안으로 그대로 숨기지 않는다.

## 게임 시스템 아키텍처 (필수)

- **몬스터 Behavior Tree:** Sequence / Selector / Action 노드.
- **SO 기반 Action:** 각 Action 로직은 ScriptableObject — 인스펙터에서 할당/교체.
- **Blackboard:** 몬스터 상태·타겟·동적 변수 공유용. `[Serializable]` 클래스로 `Monster`/`BehaviorTree`가 필드로 소유, BT 노드의 `Execute(BlackBoard _refBB)`로 전달. 파일은 `BlackBoard.cs`로 분리.
- **데이터 분리 (최우선):** SO는 '데이터와 에디터 세팅'만. 런타임 인스턴스별 상태는 **반드시 Blackboard나 런타임 노드 인스턴스**에. 예외: BT 진입 시 `Instantiate`로 몬스터별 클론되는 Composite 노드(Sequence/Select)는 클론 인스턴스에 한해 인덱스/타이머를 들고 있어도 됨. 클론되지 않는 leaf Action은 절대 상태를 갖지 말 것.
- **Object Pool:** Bullet/FX/Enemy 필수 — [[object-pooling]] (`SOPoolData` + `PoolObject` + `ObjectPoolManager`).
- **Event System:** 점수/피격/게임오버/레벨업 UI/조커 카드 UI 등 UI↔시스템 느슨한 결합은 R3 `Subject<T>`/`Observable<T>`. 인스펙터 바인딩이 필요한 UI 클릭은 `UnityEvent` 허용.

## 싱글톤 (절제)

- **허용:** 씬에 하나만 있어야 하고 여러 시스템이 공통 참조하는 매니저급 — `InputManager`, `ObjectPoolManager`, `AudioManager`, `CameraManager` 등
- **금지:** `PlayerSystem`/`ScoreSystem`처럼 기능 하나짜리 클래스의 습관적 싱글톤화 (→ `[SerializeField]` 직접 참조), 모든 걸 다 가진 `GameManager`(→ 갓 오브젝트 금지)
- `public static T m_Instance { get; private set; }` **프로퍼티**로 선언한다. `public static T m_Instance;` 처럼 raw 필드로 노출하지 말 것 — 외부에서 실수로 재할당할 수 있다. 이름은 프로젝트 전반(`BattleManager`, `FeatureManager`, `ObjectPoolManager` 등)과 통일해 `m_Instance` 를 쓴다
- Awake 가드에서 **`return;` 필수** — `Destroy`는 프레임 끝까지 지연되므로 없으면 파괴 예정 인스턴스가 `m_Instance`를 덮어쓴다:
- 부트스트랩 순서와 수명이 보장된 뒤에는 `ObjectPoolManager.m_Instance` 같은 싱글톤을 호출할 때마다 null 체크하지 않는다. 초기화 순서가 보장되지 않는 위치라면 호출부 방어를 늘리지 말고 부트스트랩이나 실행 순서를 고친다.

```csharp
private void Awake()
{
    if (m_Instance != null && m_Instance != this) { Destroy(gameObject); return; }
    m_Instance = this;
    DontDestroyOnLoad(gameObject);   // 루트 오브젝트에서만 동작 — 자식이면 조용히 무시됨 (known-issues 참고)
}
```

## 시스템 간 통신 — R3 Subject

```csharp
public sealed class Monster : MonoBehaviour
{
    private static readonly Subject<int> m_subjectDied = new();    // 구독자가 발행자 인스턴스를 몰라도 되게 static
    public static Observable<int> OnMonsterDied => m_subjectDied;  // 외부엔 Observable 로만 (OnNext 못 하게)
    private void Die() => m_subjectDied.OnNext(m_SOInfo.ExpReward);
}

public sealed class BattleManager : MonoBehaviour
{
    private IDisposable m_disposableDied;
    private void OnEnable()  => m_disposableDied = Monster.OnMonsterDied.Subscribe(AddExp);
    private void OnDisable() => m_disposableDied?.Dispose();      // 짝 필수 — static Subject 는 씬 전환에도 살아남음
}
```

- 구독 수명은 셋 중 하나로 반드시 묶는다: 오브젝트 수명 = `.AddTo(this)` / `OnEnable`↔`OnDisable` = `IDisposable` 필드(여러 개면 `DisposableBag` + `Clear()`) / `CancellationToken` = `RegisterTo(ct)`
- 인자 없는 알림은 `Subject<Unit>` + `OnNext(Unit.Default)`, 인자 2개 이상은 명명 튜플 `Subject<(SOData refData, SlotView refSlot)>`
- 매니저 싱글톤 직접 호출(`ObjectPoolManager.m_Instance.Get(...)`)은 허용 — Observable 은 "발행자가 구독자를 몰라야 할 때"만

## 값 바인딩 — R3

HP/점수/카드 수처럼 **계속 변하고 여러 View가 지켜보는 값**은 Model/System이 `ReactiveProperty<T>`로 소유하고 View는 `Subscribe(...).AddTo(this)`로 표시만 갱신한다. 1회성 알림은 위의 `Subject<T>`. 패턴·규칙·UniRx와의 차이는 [[r3]] 스킬.

## 비동기 — UniTask

`StartCoroutine`/`IEnumerator`/`yield return`/`async void` 금지.

```csharp
private readonly CancellationTokenSource m_cts = new();

private async UniTaskVoid StartSpawning()               // fire-and-forget 은 UniTaskVoid, 대기 가능하면 UniTask
{
    for (int i = 0; i < 10; i++)
    {
        await SpawnWave(i, m_cts.Token);
        await UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: m_cts.Token);
    }
}

private void OnDestroy() => m_cts.Cancel();
```

- 항상 `CancellationToken` 전달 (`this.GetCancellationTokenOnDestroy()` 또는 자체 CTS)
- 병렬은 `UniTask.WhenAll`, 백그라운드에서 복귀는 `UniTask.SwitchToMainThread()`

## 상속보다 조합

MonoBehaviour 상속 최대 깊이 2 (베이스 + 서브클래스 1). 그 이상은 조합. View는 가볍게 — 로직은 System, 데이터는 Model.

## UI = MVP + 기능 분리

화면 오케스트레이터(Presenter, 예: `PlayerStatUI`)는 **조합/위임만** 한다. 표시 단위는 별도 View 컴포넌트(`StatDetailView`, `UpgradeButtonView`)로 쪼개 `[SerializeField]`로 소유하고 이벤트 구독/메서드 호출로 위임한다 — `DungeonManager`가 스폰을 `ObjectSpawner`에 위임하는 것과 같은 패턴.

```csharp
public sealed class PlayerStatUI : MonoBehaviour
{
    [SerializeField] private Container m_refContainer;
    [SerializeField] private StatDetailView m_refDetailView;      // 선택 스탯 표시 전담
    [SerializeField] private UpgradeButtonView m_refUpgradeView;  // 비용/재화 비교/알파 전담

    private void Awake()
    {
        m_refContainer.OnSelectEvt += m_refDetailView.Show;
        m_refContainer.OnSelectEvt += m_refUpgradeView.Bind;
    }
}
```

- View는 `Show()`/`Refresh()`류만 — 로직 없음
- 판단 기준: "다른 화면에서도 재사용될 독립 표시/기능 단위"면 컴포넌트로 분리. 이 화면 전용 배선만이면 오케스트레이터에 남겨도 됨
- `ICountable` 같은 인터페이스 책임도 오케스트레이터가 직접 들기보다 전용 컴포넌트 분리를 먼저 검토

## 정적 데이터 = ScriptableObject

아이템, 어빌리티, 적 설정, 스테이지, BT Action, 풀 데이터는 SO. **런타임에 변하는 상태는 절대 SO에 넣지 말 것** — Model/Blackboard/Manager 배열에.

## 입력 시스템 아키텍처

입력은 여러 System이 각자 구독하는 전역 관심사이므로 `InputManager`만 예외적으로 싱글톤.

- `.inputactions` 에셋(`Assets/3D/06_Input/PlayerAction.inputactions`)에서 **"Generate C# Class"를 켜서** 생성된 클래스(`PlayerAction`)를 `InputManager`가 `new` 해서 소유한다. 액션 추가 = 에셋에 추가 → 클래스 자동 재생성 → 코드 한 줄. 인스펙터 드래그 없음, 이름 변경은 컴파일 에러로 드러남. (예전 `List<InputActionReference>` 방식은 액션 하나마다 필드·인스펙터·Update 메서드가 늘어나 폐기)
- `InputManager`는 **입력을 읽어 노출하는 것까지만**. 이동 계산·발사 로직은 각 System(`PlayerMovement`, `WeaponSystem`)이 `InputManager.m_Instance`를 읽어 처리
- **연속 입력 ↔ 불연속 입력을 반드시 다르게 다룬다:**
  - **연속**(이동 방향, 마우스 위치/델타) → `Update`에서 `ReadValue`로 읽어 `tInputInfo`에 캐싱, 소비자는 `InputInfo`에서 값을 읽는다. 매 프레임 값이 필요하므로 폴링이 맞다.
  - **불연속**(버튼: 점프/롤/발사/상호작용) → **캐싱도 폴링도 하지 않는다.** `performed` 콜백을 받아 `Subject<Unit>`으로 발행하고, 소비자는 `Observable<Unit>`을 구독한다.
  - 이유: 불연속 입력을 `bool`로 캐싱하면 키 하나당 `tInputInfo` 필드 + `Update` 읽기 + 소비자 폴링이 같이 늘어 **키 개수에 비례해 Update가 무거워진다**. 이벤트는 실제로 눌린 프레임에만 비용이 든다.
  - 주의: 이벤트는 **누른 순간 1회**만 온다. 누르고 있는 동안 반복이 필요하면 `.inputactions`의 해당 액션에 Hold/Repeat 인터랙션을 추가한다 — 소비자에서 폴링으로 되돌리지 말 것.
- `InputManager` 내부: `OnEnable`에서 `Enable()` + `performed` 구독, `OnDisable`에서 해제 + `Disable()` 짝 필수
- **소비자 쪽 구독은 `Start`에서 `.AddTo(this)`로** (해제는 파괴 시 자동). `Awake`/`OnEnable`은 오브젝트 간 순서가 보장되지 않아 `InputManager.m_Instance`가 아직 null일 수 있다 — `Start`는 모든 `Awake` 이후라 안전하다. (`Player.cs`가 `BattleManager.Exp`를 구독하는 기존 패턴과 동일)

```csharp
public sealed class InputManager : MonoBehaviour
{
    public static InputManager m_Instance { get; private set; }

    private PlayerAction m_refActions;                 // 생성된 클래스
    public Vector2 MoveDir { get; private set; }       // 연속 입력: 캐싱해서 노출
    private readonly Subject<Unit> m_subjectMoveButton = new();
    public Observable<Unit> OnMoveButtonPressed => m_subjectMoveButton;   // 불연속 입력: 눌린 순간에만 발행

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this) { Destroy(gameObject); return; }
        m_Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 생성은 Awake 가 아니라 OnEnable 에서 한다 (둘 다 실제로 터졌던 버그):
    // (1) Play 중 재컴파일(도메인 리로드) 시 Awake 는 재호출되지 않는데 m_refActions 는
    //     직렬화 대상이 아니라 null 로 리셋된다 → Awake 에서만 만들면 입력이 영구히 죽는다
    // (2) Awake 가드로 파괴 예약된 중복 인스턴스도 그 프레임 끝까지 OnEnable/Update 가 더 돈다
    private void OnEnable()
    {
        if (m_refActions == null)
            m_refActions = new PlayerAction();

        m_refActions.MoveAction.Enable();
        m_refActions.MoveAction.MoveButton.performed += OnMoveButtonPerformed;
    }

    private void OnDisable()
    {
        m_refActions.MoveAction.MoveButton.performed -= OnMoveButtonPerformed;
        m_refActions.MoveAction.Disable();
    }

    private void Update() => MoveDir = m_refActions.MoveAction.Move.ReadValue<Vector2>();

    private void OnMoveButtonPerformed(InputAction.CallbackContext _tContext) => m_subjectMoveButton.OnNext(Unit.Default);
}

// --- 소비자: 연속값은 읽고, 버튼은 구독한다 ---
public sealed class PlayerMovement : MonoBehaviour
{
    private void FixedUpdate()
    {
        Vector2 vMoveDir = InputManager.m_Instance.MoveDir;   // 연속 - 폴링
        // 이동 로직은 여기(System), InputManager 엔 없음
    }
}

public sealed class Player : MonoBehaviour
{
    // Awake/OnEnable 이 아니라 Start - m_Instance 가 준비된 것이 보장되는 시점. AddTo(this) 가 OnDestroy 해제를 대신한다
    private void Start() => InputManager.m_Instance.OnMoveButtonPressed.Subscribe(_ => MoveRoll()).AddTo(this);

    private void MoveRoll() { /* 눌린 프레임에만 호출된다 */ }
}
```

액션 맵 전환(게임플레이 ↔ UI)은 현재 맵을 `Disable()`한 **뒤에** 다음 맵을 `Enable()`. 여러 게임플레이 맵을 동시에 켜두지 말 것.

## 갓 오브젝트 금지

점수·목숨·스폰·UI·오디오·저장·입력을 한 클래스가 처리하지 말 것. 책임별 System(`PlayerSystem`, `ScoreSystem`, `SpawnSystem`)으로 나누고 씬에서 `[SerializeField]`로 필요한 것만 연결.

## 씬 구성

- 부트스트랩(첫) 씬에 매니저급 싱글톤 배치 + `DontDestroyOnLoad` (반드시 **루트** 오브젝트 — 자식이면 무시됨)
- 게임 씬은 그 위에서 필요한 System/View만
- 씬 로드는 `await SceneManager.LoadSceneAsync("GameScene", LoadSceneMode.Additive).ToUniTask();` 또는 Addressables
