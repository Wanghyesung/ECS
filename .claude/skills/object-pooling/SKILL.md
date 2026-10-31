---
name: object-pooling
description: "이 프로젝트 전용 오브젝트 풀링 구조 — SOPoolData(데이터 SO) + PoolObject(IPoolable) + ObjectPoolManager(싱글톤). Addressables 프리워밍, 상시 풀/씬 풀 수명 분리, PriorityQueue 기반 자동반납, Generation 가드, ActiveCap. 탄·이펙트·몬스터 생성/반납 코드를 쓸 때 사용합니다."
alwaysApply: true
---

# 오브젝트 풀링 — SOPoolData + PoolObject + ObjectPoolManager

탄, 이펙트, 몬스터처럼 자주 생기고 사라지는 오브젝트는 전부 이 풀을 거친다. `Instantiate`/`Destroy` 직접 호출 금지.

## 구성

| 타입 | 역할 |
|---|---|
| `SOPoolData` (데이터 SO) | 풀 하나의 설정 — `PrefabRef`(Addressable), `PreLoad`(미리 만들 개수), `ActiveCap`(동시 활성 상한, 0 이하면 무제한), `Max`(미사용) |
| `SOPoolDataSet` | 상시 풀 목록. `ObjectPoolManager` 인스펙터의 `m_refStaticPoolDataSet` |
| `SOSceneData.ScenePoolDataList` | 그 씬에서만 쓰는 풀 목록 |
| `PoolObject` (`IPoolable`) | 풀 대상 프리팹 루트에 붙는 컴포넌트. `PoolKey`, `PushCount`, `Generation`, `OnPush`/`OnPop` |
| `ObjectPoolManager` | 싱글톤(DDOL). 로드·인출·반납·자동반납 예약 |

- 풀 키는 `SOPoolData.PrefabRef.AssetGUID` **문자열**이다. 빌드에서 SO 사본이 여러 벌 생겨도 같은 키로 모인다
- **상시 풀**(플레이어 탄 등)은 매니저 자식으로 남고, **씬 풀**은 씬 전환 때 파괴된다

## 로드 흐름

`GameSceneManager.LoadSceneAsync`가 이 순서로 부른다:

```csharp
ObjectPoolManager.m_Instance.SceneChange();                                          // 살아있는 상시 오브젝트 반납 + 씬 풀 정리
await ObjectPoolManager.m_Instance.LoadStaticPoolDataAsync(tToken, refProgress);    // 상시 풀 (최초 1회)
// ... 씬 로드 ...
await ObjectPoolManager.m_Instance.ReplaceScenePoolsAsync(_refSceneData.ScenePoolDataList, tToken, refProgress);
```

테스트 씬처럼 목록만 바로 올릴 때는 `LoadPoolAsync(listPoolData, tToken)`.

## 인출 · 반납

```csharp
[SerializeField] private SOPoolData m_refBulletPoolData;   // 풀 키는 m_ref (csharp-unity.md §2)

GameObject refObj = ObjectPoolManager.m_Instance.GetObject(m_refBulletPoolData, vSpawnPos);
if (refObj == null)
    return;   // 풀 미등록이거나 고갈 — 새로 만들지 않고 조용히 실패한다

ObjectPoolManager.m_Instance.PushObject(refObj);          // 즉시 반납 (중복 반납은 PushCount 로 걸러짐)
m_refPoolObj.SetAliveTime(m_refAttackInfo.AliveTime);     // n초 뒤 자동 반납 예약
```

- `SetActive(false)`만 하면 풀로 돌아가지 않는다 — 반드시 `PushObject` 또는 `SetAliveTime`
- 원본 프리팹 값(forward 등)이 필요하면 `GetPoolPrefab(SOPoolData)` → 프리팹의 `PoolObject`
- 남은 개수는 `GetObjectCount(SOPoolData)` (-1 = 미등록)

## 자동 반납과 Generation

- `PoolObject.m_fAliveTime`(인스펙터, 기본 3초)이 0보다 크면 `Pop()` 때 자동 반납이 예약된다. **0 이하 = 수동 반납만**
- 예약은 `PriorityQueue`에 "이 시각에 반납"으로만 들어가고, 매니저가 맨 앞만 확인한다 (오브젝트마다 타이머를 돌리지 않음)
- `Pop()` / `SetAliveTime()` / `SuspendLifetime()`은 `Generation`을 올린다. 예약 당시 Generation 과 다르면 그 예약은 버려진다 — 반납 후 재사용된 오브젝트를 옛 예약이 잘못 반납하지 않게
- 발사 준비 중처럼 자동 반납을 잠시 막아야 하면 `SuspendLifetime()` (`Weapon.cs` 참고)

## OnPush / OnPop 구독 (R3)

반납·인출 시점에 초기화를 끼워 넣을 때. 구독은 `OnEnable`↔`OnDisable` 짝으로.

```csharp
private IDisposable m_disposablePush;

private void OnEnable()
{
    m_disposablePush = m_refPoolObj.OnPush.Subscribe(_ => m_refTrail.Clear());
}

private void OnDisable()
{
    m_disposablePush?.Dispose();
}
```

구독이 여러 개면 `DisposableBag m_bagEvents` + `AddTo(ref m_bagEvents)` + `OnDisable`에서 `m_bagEvents.Clear()` (`Bullet.cs` 참고).

## ActiveCap

`SOPoolData.ActiveCap > 0`이면 활성 개수가 상한에 닿았을 때 **가장 오래된 활성 인스턴스를 강제로 반납**하고 자리를 만든다. 피격 이펙트처럼 폭주하면 프레임이 무너지는 풀에 건다.

## 새 풀 추가 순서

1. 프리팹 루트에 `PoolObject`를 붙이고 Addressable 로 등록
2. `SO_<이름>` `SOPoolData` 에셋 생성 — `PrefabRef`, `PreLoad`(동시에 필요한 최대 수), 필요하면 `ActiveCap`
3. 상시 풀이면 `SOPoolDataSet`에, 씬 전용이면 그 씬의 `SOSceneData` 목록에 추가 — 빠뜨리면 `GetObject`가 항상 null
4. 쓰는 쪽에 `[SerializeField] private SOPoolData m_ref<이름>PoolData;`

## 주의

- 풀 대상에 `Destroy()` 금지 — 파괴는 매니저의 풀 정리(`SceneChange`/`ClearPool`)에서만 일어난다
- `GetObject`의 null 을 무시하면 스폰이 에러 없이 사라진다. 고갈이 잦으면 `PreLoad`를 늘린다
