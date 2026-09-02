---
name: unirx
description: "UniRx(neuecc) — Unity를 위한 Reactive Extensions(Rx) 서드파티 라이브러리. Observable 스트림, IDisposable 구독 관리, MonoBehaviour 이벤트의 Observable화. 'UniRx', 'Observable', '반응형 스트림' 언급 시 사용합니다."
globs: ["**/UniRx*", "**/*Observable*.cs"]
---

# UniRx — Unity를 위한 Reactive Extensions (서드파티)

[neuecc/UniRx](https://github.com/neuecc/UniRx)는 .NET Reactive Extensions(Rx)를 Unity에 최적화해 이식한 라이브러리다. 값의 변화를 스트림(Observable)으로 다루고, LINQ 스타일 연산자(`Where`, `Select`, `Throttle`, `DistinctUntilChanged` 등)로 합성한다.

## 이 프로젝트에서의 위치

- 비동기 흐름 제어(대기, 딜레이, 코루틴 대체)는 표준대로 **UniTask**([[unitask]])를 사용한다. UniRx를 UniTask 대신 쓰지 않는다 — UniRx의 async 대체 기능(구 UniRx.Async)은 이 프로젝트에서 쓰지 않는다.
- UniRx는 **연속적으로 변하는 값을 관찰하고 합성**해야 하는 경우에만 검토한다 — 예: 여러 UI가 동시에 구독하는 인벤토리 슬롯 값, 여러 소스를 조합해야 하는 반응형 값, 입력 스트림 디바운스/스로틀.
- 시스템 간 느슨한 결합의 기본은 여전히 [[architecture]]에 정의된 `event Action<T>`다. UniRx는 "값 스트림"이 필요할 때의 보조 도구일 뿐, 단순 1회성 알림 이벤트까지 전부 Observable로 바꾸지 않는다.

## 핵심 패턴

### ReactiveProperty — 관찰 가능한 값

```csharp
public sealed class PlayerHealth : MonoBehaviour
{
    public readonly ReactiveProperty<int> CurrentHp = new ReactiveProperty<int>(100);

    private readonly CompositeDisposable m_disposables = new CompositeDisposable();

    private void OnEnable()
    {
        CurrentHp
            .Where(_iHp => _iHp <= 0)
            .Subscribe(_ => OnDeath())
            .AddTo(m_disposables);
    }

    private void OnDisable() => m_disposables.Clear();

    private void OnDestroy() => m_disposables.Dispose();

    public void TakeDamage(int _iAmount) => CurrentHp.Value -= _iAmount;

    private void OnDeath() { /* ... */ }
}
```

### 구독 생명주기 — 반드시 AddTo로 관리

```csharp
// 나쁜 예 — 구독을 저장하지 않음, 오브젝트가 파괴돼도 콜백이 남을 수 있음
someObservable.Subscribe(_ => DoSomething());

// 좋은 예 — this가 파괴될 때 자동으로 구독 해제됨
someObservable.Subscribe(_ => DoSomething()).AddTo(this);

// 좋은 예 — 여러 구독을 한 번에 관리
private readonly CompositeDisposable m_disposables = new CompositeDisposable();
someObservable.Subscribe(_ => DoSomething()).AddTo(m_disposables);
private void OnDestroy() => m_disposables.Dispose();
```

### MonoBehaviour 이벤트를 Observable로

```csharp
this.OnTriggerEnterAsObservable()
    .Where(_col => _col.CompareTag("Player"))
    .Subscribe(_col => OnPlayerEntered(_col))
    .AddTo(this);
```

입력은 프로젝트 표준([[architecture]]의 InputManager)을 그대로 사용한다 — 레거시 `Input` 이벤트를 Observable로 감싸지 않는다.

### 디바운스 / 스로틀

```csharp
searchInputField.onValueChanged.AsObservable()
    .Throttle(TimeSpan.FromMilliseconds(300))
    .DistinctUntilChanged()
    .Subscribe(_strQuery => PerformSearch(_strQuery))
    .AddTo(this);
```

### 여러 스트림 합성

```csharp
Observable.CombineLatest(hpStream, manaStream, staminaStream,
        (_iHp, _iMana, _iStamina) => _iHp > 0 && _iMana > 0 && _iStamina > 0)
    .DistinctUntilChanged()
    .Subscribe(_bCanAct => canActGauge.SetActive(_bCanAct))
    .AddTo(this);
```

## 안티패턴

- `Subscribe` 결과를 버리지 마라 — 반드시 `AddTo`로 수명 주기에 묶는다. 안 그러면 파괴된 오브젝트를 참조하는 구독이 계속 살아남아 `MissingReferenceException`으로 이어진다.
- 매 프레임 새로운 Observable 체인(`Where().Select()...`)을 만들지 마라 — `Awake`/`OnEnable`에서 한 번만 구성하고 재사용한다. 핫 패스에서 매번 새로 구성하면 힙 할당이 발생한다([[performance]] 위반).
- 단순 1회성 알림(점수 갱신, 사망 이벤트)까지 ReactiveProperty로 바꾸지 마라 — [[architecture]] 표준인 `event Action<T>`가 더 단순하다.

## 참고 자료

- 공식 저장소: https://github.com/neuecc/UniRx
- 정확한 연산자 목록이나 최신 설치 방법이 필요하면 WebFetch로 위 저장소의 README를 직접 읽어라.
