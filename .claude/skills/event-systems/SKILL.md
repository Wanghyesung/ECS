---
name: event-systems
description: "시스템 간 알림 방식 선택 가이드 — R3 Subject/Observable(1회성), ReactiveProperty(계속 변하는 값), UnityEvent(인스펙터 전용), 매니저 직접 호출. 구독 수명(AddTo/Dispose/DisposableBag), 구독 시점(Awake/Start), 흔한 누수 실수를 다룹니다. C# event 는 새로 만들지 않습니다."
alwaysApply: true
---

# 이벤트 / 알림 — R3 기준

2026-09-15 결정으로 프로젝트의 `event Action`은 전부 R3로 옮겼다. **C# `event`, SO 이벤트 채널, static EventBus 는 새로 만들지 않는다.** R3 API 자체는 [[r3]] 스킬.

## 무엇을 쓸까

| 상황 | 쓰는 것 | 예 |
|---|---|---|
| 1회성 알림, 발행자가 구독자를 몰라야 함 | `Subject<T>` 소유 + `Observable<T>` 노출 | `Monster.OnMonsterDied`, `PoolObject.OnPush`, `BaseCollider.OnHitTargetEnter` |
| 계속 변하고 여러 View 가 지켜보는 값 | `ReactiveProperty<T>` 소유 + `ReadOnlyReactiveProperty<T>` 노출 | `BattleManager.Exp`, `DungeonManager.IsBossCutscene` |
| 디자이너가 인스펙터에서 연결하는 반응 (버튼 클릭 등) | `UnityEvent` | |
| 발행자·수신자가 항상 같이 있고 같은 프레임에 끝남 | 직접 메서드 호출 / 매니저 싱글톤 호출 | `ObjectPoolManager.m_Instance.GetObject(...)` |

## 발행자

```csharp
// 발행자 인스턴스를 구독자가 모르는 전역 알림 → static
private static readonly Subject<int> m_subjectDied = new Subject<int>();
public static Observable<int> OnMonsterDied => m_subjectDied;     // 외부엔 Observable 로만 (OnNext 못 하게)

// 특정 인스턴스의 알림 → 인스턴스 멤버
private readonly Subject<SOData> m_subjectSelect = new Subject<SOData>();
public Observable<SOData> OnSelectEvt => m_subjectSelect;

// 값 → 백킹과 노출 분리
private readonly ReactiveProperty<bool> m_rpIsBossCutscene = new ReactiveProperty<bool>(false);
public ReadOnlyReactiveProperty<bool> IsBossCutscene => m_rpIsBossCutscene;
```

- 인자 없는 알림은 `Subject<Unit>` + `OnNext(Unit.Default)`
- 인자 2개 이상은 명명 튜플 `Subject<(SOData refData, SlotView refSlot)>`

## 구독 수명 — 셋 중 하나로 반드시 묶는다

```csharp
// 1) 오브젝트 수명 — 파괴될 때 자동 해제
m_refContainer.OnSelectEvt.Subscribe(SelectStat).AddTo(this);

// 2) 켜져 있는 동안만 — OnEnable/OnDisable 짝
private IDisposable m_disposablePush;

private void OnEnable()
{
    m_disposablePush = m_refPoolObj.OnPush.Subscribe(_ => ResetState());
}

private void OnDisable()
{
    m_disposablePush?.Dispose();
}

// 2-1) 여러 개면 DisposableBag
private DisposableBag m_bagEvents;

private void OnEnable()
{
    Monster.OnMonsterDied.Subscribe(MonsterDead).AddTo(ref m_bagEvents);
    m_refSpawner.OnSpawned.Subscribe(HandleSpawned).AddTo(ref m_bagEvents);
}

private void OnDisable()
{
    m_bagEvents.Clear();
}

// 3) CancellationToken 수명 — RegisterTo(tToken)
```

## 구독 시점

- 같은 씬의 다른 오브젝트(특히 싱글톤)를 구독할 때는 **`Start`** — `Awake`/`OnEnable`은 오브젝트 간 순서가 보장되지 않아 `m_Instance`가 아직 null 일 수 있다
- 발행자가 이전 씬에서 넘어온 DDOL 매니저라 이미 존재가 보장되면 `Awake`에서 구독해도 된다 (`BossWarningView` → `DungeonManager`)
- 풀링 오브젝트처럼 꺼졌다 켜지며 재사용되는 쪽은 `OnEnable`/`OnDisable` 짝

## 흔한 실수

1. **해제를 빼먹음** → 파괴된 오브젝트에서 콜백이 돈다. static `Subject`는 씬 전환에도 살아남으므로 특히 위험
2. **`Subject`를 public 으로 노출** → 아무나 `OnNext`를 부른다. 노출은 항상 `Observable<T>`
3. **1회성 알림에 `ReactiveProperty`** → 구독하는 순간 현재 값이 한 번 즉시 흘러온다. 그 동작이 필요할 때만 쓴다
4. **`Update` 안에서 `Subscribe`** → 매 프레임 구독이 쌓이고 할당된다. 구독은 수명 시작점에서 한 번
5. **같은 프레임에 끝나는 1:1 호출에 이벤트** → 직접 메서드 호출이 더 단순하다
