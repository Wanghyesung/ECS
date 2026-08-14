---
name: object-pooling
description: "이 프로젝트 전용 오브젝트 풀링 구조 — SOPoolData(SO) + PoolObject(IPoolable) + ObjectPool(싱글톤). Addressables 프리워밍, PriorityQueue 기반 자동반납, Generation 가드. 런타임 Instantiate/Destroy 오버헤드를 제거합니다."
alwaysApply: true
---

# 오브젝트 풀링 (Object Pooling)

`Instantiate()`를 호출할 때마다 메모리가 할당되고, `Destroy()`를 호출할 때마다 GC가 발생합니다. 자주 생성하고 파괴하는 오브젝트는 풀링하세요: 발사체, 파티클, 적, 픽업 아이템 등.

> **이 프로젝트는 Unity 내장 `UnityEngine.Pool.ObjectPool<T>`를 쓰지 않습니다.** 아래 3개 클래스로 구성된 자체 풀링 구조(`Assets/3D/05_Manager/Pool/`)를 표준으로 사용하세요.

## 구성 요소

### `SOPoolData` (ScriptableObject)

무엇을 얼마나 프리로드할지 정의하는 순수 데이터. `PrefabRef`는 반드시 Addressable이어야 합니다.

```csharp
[CreateAssetMenu(fileName = "SO_PoolData", menuName = "Game/Load/PoolData")]
public class SOPoolData : ScriptableObject
{
    public AssetReferenceGameObject PrefabRef;
    public int PreLoad = 8; // 씬 진입 시 미리 인스턴스화할 개수
    public int Max = 12;    // 아직 사용하지 않음
}
```

### `PoolObject` (MonoBehaviour, `IPoolable`)

풀링 대상 프리팹의 루트에 부착합니다. `Push()`/`Pop()`이 풀 반납/인출 생명주기이며, `Generation` 카운터로 "낡은 자동반납 예약"을 무시합니다.

```csharp
public interface IPoolable
{
    public PoolObject PoolKey { get; }
    public int PushCount { get; }
    public void SetOriginalPoolObj(PoolObject _refOriginObj);
    public void Push();
    public void Pop();
}
```

- `PoolKey`는 **원본 프리팹의 `PoolObject`**를 가리킵니다(인스턴스 자신이 아님) — `ObjectPool`이 `Dictionary<PoolObject, Queue<GameObject>>`를 원본 프리팹 기준으로 관리하기 때문입니다.
- `m_fAliveTime` (기본 3초)이 0보다 크면 `Pop()` 시점에 자동으로 `ObjectPool.m_Instance.ScheduleTime(this, m_fAliveTime)`이 예약됩니다. **0 이하로 설정하면 "수동으로만 반납"하겠다는 의도**이므로 자동 예약이 걸리지 않습니다.
- `OnPush`/`OnPop` C# 이벤트로 풀 반납/인출 시점에 부가 동작(트레일 초기화, 이펙트 리셋 등)을 끼워 넣으세요. 아래 확장 예시 참고.

### `ObjectPoolManager` (싱글톤, DontDestroyOnLoad)

```csharp
public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPool m_Instance = null;
    private Dictionary<PoolObject, Queue<GameObject>> m_hashPool = new();
    private Dictionary<PoolObject, AsyncOperationHandle> m_hashHandle = new();
    private PriorityQueue<tTimeData> m_PQTimer; // 자동반납 예약
    // ...
}
```

- Addressables + UniTask로 프리팹을 비동기 로드하고, `InstantiateAsync`로 `PreLoad` 개수만큼 프레임 분산 인스턴스화합니다.
- 자동반납은 오브젝트마다 매 프레임 카운트다운하지 않습니다. **PriorityQueue에 "이 시각에 반납"만 예약**해두고, 매 프레임 큐 맨 앞(가장 이른 만료 시각) 하나만 확인합니다 — `ObjectSpawner`와 동일한 패턴. 오브젝트 수가 늘어도 매 프레임 비용이 늘지 않습니다.
- 예약 시점에 저장해둔 `Generation`이 실제 오브젝트의 현재 `Generation`과 다르면 그 예약은 버려집니다 — 예약 이후 수동으로 Push→Pop이 다시 일어나 이미 새 생애가 시작된 경우, 낡은 예약이 새 생애를 잘못 반납시키는 것을 막기 위함입니다.

## 사용 흐름

1. 풀링할 프리팹 루트에 `PoolObject` 컴포넌트를 붙이고 `m_fAliveTime`을 설정합니다 (자동반납이면 양수, 수동반납이면 0 이하).
2. `SO_PoolData` 에셋을 만들어 `PrefabRef`(Addressable)와 `PreLoad` 개수를 지정합니다.
3. 씬 진입 시 (보통 SceneController에서) `SOPoolData` 목록을 모아 프리워밍합니다:
   ```csharp
   await ObjectPool.m_Instance.LoadPoolAsync(listPoolData, token, progress);
   ```
4. 스폰할 때는 **원본 프리팹의 `PoolObject`**를 키로 넘겨 꺼냅니다:
   ```csharp
   GameObject refBullet = ObjectPool.m_Instance.GetObject(m_refBulletPoolKey, vSpawnPos);
   ```
5. 반납은 `m_fAliveTime`에 의한 자동 반납을 기본으로 쓰고, 즉시 반납이 필요하면 직접 호출합니다:
   ```csharp
   ObjectPool.m_Instance.PushObject(refGameObj);
   ```
   `PushObject`는 이미 반납된 오브젝트(`PushCount > 0`)를 걸러내므로 중복 반납은 안전합니다.

## `OnPush`/`OnPop`으로 확장하기

풀 반납/인출 부가 동작은 `PoolObject`를 상속하거나 수정하지 말고, 별도 컴포넌트에서 이벤트를 구독하세요. 실제 예시 (`Assets/3D/13_Uti/PoolTrailReset.cs`) — 반납되는 순간 `TrailRenderer`를 비워 잔상을 방지합니다:

```csharp
public class PoolTrailReset : MonoBehaviour
{
    private PoolObject m_refPoolObj;
    private TrailRenderer m_refTrail;

    private void Awake()
    {
        m_refPoolObj = GetComponentInChildren<PoolObject>();
        m_refTrail = GetComponentInChildren<TrailRenderer>();
    }

    private void OnEnable() => m_refPoolObj.OnPush += m_refTrail.Clear;
    private void OnDisable() => m_refPoolObj.OnPush -= m_refTrail.Clear;
}
```

같은 패턴으로 이펙트 상태 초기화, 데미지 플래그 리셋, 타이머 초기화 등을 필요한 컴포넌트마다 나눠서 구독하세요.

## 규칙

- **`Destroy()`를 직접 호출하지 마세요** — 풀링 대상은 `ObjectPool.PushObject`로만 반납합니다. `Destroy`는 `ClearPool()`(씬 전환 시 풀 전체 해제)에서만 일어납니다.
- **풀 키는 항상 원본 프리팹의 `PoolObject`** — 인스턴스마다 다른 `PoolObject`를 키로 쓰면 `m_hashPool`에서 찾지 못합니다. `SetOriginalPoolObj`로 인스턴스 생성 시 자동 연결됩니다.
- **`m_fAliveTime` 재설정은 `SetAliveTime()`으로** — 직접 필드를 바꾸지 말고 `SetAliveTime()`을 호출해야 `Generation`이 올라가면서 기존 예약이 무효화되고 새 시간으로 재예약됩니다.
- **`OnPush`/`OnPop` 구독은 반드시 `OnEnable`/`OnDisable`로 짝을 맞추세요** — 다른 C# 이벤트 규칙과 동일합니다.
- Addressables 핸들은 `ObjectPool`이 `m_hashHandle`로 소유·해제합니다. 개별 스크립트에서 별도로 `Addressables.Release`를 호출하지 마세요.

## 언제 풀링해야 하는가

**풀링해야 하는 것:**
- 발사체 (총알, 화살, 미사일)
- 파티클 이펙트
- 웨이브 기반 게임의 적
- 픽업 아이템
- 데미지 숫자 / 플로팅 텍스트
- 트레일 렌더러를 쓰는 오브젝트

**풀링하지 말아야 하는 것:**
- 일회성 오브젝트 (보스, 고유 NPC)
- 한 번만 생성되는 작은 오브젝트 (데이터 컨테이너)
- 씬 전체 동안 살아있는 오브젝트

## 풀 크기 설정

- **`PreLoad`는 작게 시작하세요** — 대부분은 8~20개면 충분합니다.
- **모니터링하세요** — 게임플레이 중 `GetObjectCount`가 자주 0에 가까워지면(즉 풀 고갈) `PreLoad`를 늘리세요. `GetObject`는 풀이 비어있으면 `null`을 반환하고 새로 생성하지 않으므로, 고갈 시 스폰 자체가 조용히 실패합니다.
- **`Max`는 아직 구현되어 있지 않습니다** — 현재는 상한 없이 `Queue`에 계속 쌓입니다. 향후 상한 로직 추가 전까지는 `PreLoad` 설계로 사실상 크기를 관리하세요.
- 레벨/웨이브별로 필요한 `PreLoad` 값이 다를 수 있습니다.
