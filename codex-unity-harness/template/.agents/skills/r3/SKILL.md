---
name: r3
description: "R3(Cysharp) — UniRx의 후속 Reactive Extensions. Observable<T>, ReactiveProperty, AddTo 구독 관리, Unity 트리거/UI 이벤트의 Observable화. 'R3', 'Observable', '옵저버','ReactiveProperty', '반응형 스트림', 'UniRx' 언급 시 사용합니다."
globs: ["**/*Observable*.cs", "**/*Reactive*.cs"]
docs: "https://context7.com/cysharp/r3/llms.txt?topic={API}&tokens=3000"
---

# R3 — Unity Reactive Extensions (프로젝트 채택)

> API 가 확실치 않으면 위 `docs` URL 의 `{API}` 를 메서드/클래스명으로 바꿔 `WebFetch` — 예: `?topic=ReactiveProperty`.

[Cysharp/R3](https://github.com/Cysharp/R3)는 UniRx 저자(neuecc)가 만든 후속작이다. UniRx와 **API가 다르다** — UniRx 코드/예제를 그대로 옮기지 말 것.

## 설치 여부부터 확인

코드를 쓰기 전에 `Packages/manifest.json`에 `com.cysharp.r3`, `Assets/Packages/R3*` 또는 `Assets/Plugins/R3`가 있는지 확인한다. **없으면 코드를 쓰지 말고 아래 설치 단계를 사용자에게 안내**한다 (에이전트가 직접 설치하지 않는다).

```
1. NuGetForUnity 설치: Package Manager → Add package from git URL
   https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
2. 메뉴 NuGet → Manage NuGet Packages → "R3" 검색 → Install   (NuGet 패키지: R3 코어)
3. Package Manager → Add package from git URL
   https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity   (Unity 통합)
```

## 개념 7단계 (이 순서로 이해한다)

| 단계 | 뜻 | R3 코드 |
|---|---|---|
| **Observable** | 값을 밀어내는 스트림. 구독 전엔 아무 일도 안 함 | `Observable<int>` — UniRx `IObservable`이 아닌 자체 추상 클래스 |
| **Observer** | 받는 쪽. `OnNext / OnErrorResume / OnCompleted` 3개 | `Subscribe(_iHp => ...)` 람다가 Observer 하나로 포장됨 |
| **Subscription** | Observable ↔ Observer 연결. `Subscribe()` 반환값 `IDisposable`이 손잡이 | `IDisposable d = hp.Subscribe(...)` |
| **Operator** | 스트림을 가공해 **새 Observable**을 만드는 함수. 원본은 안 바뀜 | `.Select` `.Where` `.DistinctUntilChanged` `.ThrottleFirst` |
| **Dispose** | 연결 끊기. 안 하면 파괴된 오브젝트가 계속 콜백 받음 | `.AddTo(this)` = OnDestroy 때 자동 Dispose |
| **ReactiveProperty** | 현재 값을 기억하는 Observable. 구독 즉시 현재값 1회 발행 + `.Value` 대입 시 발행 | `ReactiveProperty<int>`, 외부엔 `ReadOnlyReactiveProperty<int>` |
| **시간/이벤트 조합** | 여러 스트림·시간을 합치는 Operator군 | `CombineLatest`(둘 다 최신값) `Merge`(아무거나) `Zip`(짝 맞춤) `Timer/Interval` `Debounce`(잠잠해진 뒤) `ThrottleFirst`(첫 것 통과 후 잠금) |

흐름: Observable 생성 → Operator 가공 → `Subscribe`(Observer 등록 = Subscription) → `Dispose`. ReactiveProperty 는 "Observable + 현재값"의 특수형.

## 적용 예시

| 용도 | 도구 |
|---|---|
| 대기/딜레이/비동기 흐름 | **UniTask** ([[unitask]]) — R3로 대체하지 않는다 |
| 시스템 간 1회성 알림 (몬스터 사망, 버튼 클릭, 풀 반납) | **`Subject<T>`** 소유 + `Observable<T>` 노출 (2026-09-15: `event Action` 전부 전환 완료) |
| **Model → View 값 바인딩** (HP, 점수, 카드 수, 레벨) | **`ReactiveProperty<T>`** — View가 `Subscribe`로 표시만 갱신 |
| 입력 스트림 가공 (디바운스/스로틀/홀드 판정) | `Observable` + `InputManager`가 노출 |
| 여러 소스를 조합한 파생 값 | `CombineLatest`, `Select` |

원칙: **계속 변하는 값은 `ReactiveProperty`, 1회성 알림은 `Subject`**. 둘 다 R3 — C# `event` 는 새로 만들지 않는다.

## UniRx → R3 차이 (자주 틀리는 것)

| UniRx | R3 |
|---|---|
| `IObservable<T>` | `Observable<T>` (자체 추상 클래스, System.IObservable 아님) |
| `OnError`로 스트림 종료 | `OnErrorResume` — 에러가 나도 **구독이 살아 있음**. 종료는 `OnCompleted(Result)` |
| `Observable.EveryUpdate()` | 동일하게 있음, 단 `UnityFrameProvider.Update` 기반 |
| `Observable.Timer/Interval(TimeSpan)` | 동일. 프레임 단위는 `TimerFrame/IntervalFrame`, `DelayFrame`, `ThrottleFirstFrame`, `DebounceFrame` |
| `.AddTo(this)` | 동일 (`AddTo(MonoBehaviour)`, `AddTo(GameObject)`, `RegisterTo(CancellationToken)`) |
| `CompositeDisposable` | 있음. 더 가벼운 `Disposable.CreateBuilder()` + `AddTo(ref builder)` 권장 |
| `ReactiveProperty<T>` (인스펙터 X) | `ReactiveProperty<T>` + **`SerializableReactiveProperty<T>`**(인스펙터 노출) |
| `ObservableTriggers` | `R3.Triggers` 네임스페이스로 동일 (`OnCollisionEnterAsObservable()` 등) |
| `button.OnClickAsObservable()` | 동일 (`R3` + UGUI 확장) |
| 스케줄러 | `TimeProvider` / `FrameProvider` (`UnityTimeProvider.Update`, `UnityFrameProvider.Update`) |
| async 연동 | `SubscribeAwait(async (x, ct) => ..., AwaitOperation.Sequential|Drop|Switch|Parallel)` |

## 핵심 패턴

### ReactiveProperty — Model이 소유, View가 구독

```csharp
// Model/System 쪽: 값 소유
public sealed class PlayerHealth : MonoBehaviour
{
    [SerializeField] private SerializableReactiveProperty<int> m_rpHp = new(100); // 인스펙터에서 초기값 조정 가능
    public ReadOnlyReactiveProperty<int> Hp => m_rpHp;                           // 외부엔 읽기 전용

    public void TakeDamage(int _iAmount) => m_rpHp.Value -= _iAmount;

    private void OnDestroy() => m_rpHp.Dispose();
}

// View 쪽: 표시만
public sealed class HpBarView : MonoBehaviour
{
    [SerializeField] private PlayerHealth m_refHealth;
    [SerializeField] private Image m_refFill;

    private void Awake()
    {
        m_refHealth.Hp
            .Select(_iHp => _iHp / 100f)
            .Subscribe(_fRatio => m_refFill.fillAmount = _fRatio)
            .AddTo(this);                                   // 파괴 시 자동 해제 — 필수
    }
}
```

### 구독 묶음 관리

```csharp
private IDisposable m_disposable;

private void OnEnable()
{
    var builder = Disposable.CreateBuilder();
    m_refModel.Score.Subscribe(OnScore).AddTo(ref builder);
    m_refModel.Combo.Subscribe(OnCombo).AddTo(ref builder);
    m_disposable = builder.Build();
}

private void OnDisable() => m_disposable?.Dispose();
```

### 입력 스트림 (InputManager가 노출, System이 가공)

```csharp
// InputManager (싱글톤): performed 이벤트를 Observable로 노출
public Observable<Unit> OnFire => m_subjectFire;          // Subject<Unit> 필드, performed 콜백에서 OnNext
public Observable<Vector2> Move => Observable.EveryUpdate().Select(_ => m_refActions.Player.Move.ReadValue<Vector2>());

// WeaponSystem: 연타 스로틀은 여기서
InputManager.Instance.OnFire
    .ThrottleFirst(TimeSpan.FromSeconds(m_fFireInterval))
    .Subscribe(_ => Fire())
    .AddTo(this);
```

### Unity 트리거 / UI

```csharp
using R3.Triggers;
this.OnTriggerEnterAsObservable()
    .Where(_refOther => ((1 << _refOther.gameObject.layer) & m_maskEnemy) != 0)   // tag 대신 layermask (프로젝트 규칙)
    .Subscribe(OnHitEnemy)
    .AddTo(this);

m_refButton.OnClickAsObservable().Subscribe(_ => Open()).AddTo(this);
```

### UniTask 와 함께

```csharp
m_refModel.LevelUp
    .SubscribeAwait(async (_iLevel, _ct) =>
    {
        await m_refCardUI.ShowAsync(_iLevel, _ct);      // UniTask 를 그대로 await
    }, AwaitOperation.Drop)                             // 연출 중 들어온 레벨업은 버림
    .AddTo(this);
```

## 규칙

- **모든 `Subscribe`는 `AddTo(this)` / `AddTo(ref builder)` / `RegisterTo(ct)` 중 하나로 반드시 수명을 묶는다.** 안 묶으면 파괴된 오브젝트가 계속 콜백을 받는다 (rules/architecture.md 이벤트 해제 규칙과 동일).
- `Subscribe`는 `Awake/OnEnable`에서 한 번. **`Update` 안에서 `Subscribe`/연산자 체인 생성 금지** (매 프레임 할당).
- `Observable.EveryUpdate()`는 델리게이트 호출이 매 프레임 붙는다 — 핫 패스(이동/발사 계산)는 그냥 `Update()`를 쓰고, R3는 "값 변화 알림"에 쓴다.
- Unity 오브젝트를 `Where`/`Select`에서 다룰 때 `?.` 쓰지 말 것 — `== null` (rules/unity-specifics.md).
- `ReactiveProperty`는 **Model/System이 소유**하고 View에는 `ReadOnlyReactiveProperty<T>`로 노출한다. View가 `.Value`를 쓰지 않는다.
- SO(ScriptableObject) 안에 `ReactiveProperty`를 두지 않는다 — 런타임 상태는 SO에 넣지 않는다는 규칙(rules/architecture.md 데이터 분리) 그대로.
- 네이밍: `m_rp<Name>` (ReactiveProperty), `m_subject<Name>` (Subject), 람다 매개변수는 `_` + 타입 접두사 (`_iHp`, `_vDir`).
- 에러: `OnErrorResume`은 스트림을 끊지 않으므로 예외를 삼키지 말 것. 처리 안 한 예외는 `ObservableSystem.RegisterUnhandledExceptionHandler`로 로깅된다 — 프로젝트 초기화 시 한 번 등록.
- 디버깅: `Window → Observable Tracker`로 살아있는 구독 수를 확인한다 (누수 검사).
