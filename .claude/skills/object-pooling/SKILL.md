---
name: object-pooling
description: "오브젝트 풀링 패턴 — Unity ObjectPool<T>, 커스텀 ComponentPool, 워밍업(사전 예열) 전략, 풀 반납 생명주기. 런타임 Instantiate/Destroy 오버헤드를 제거합니다."
alwaysApply: true
---

# 오브젝트 풀링 (Object Pooling)

`Instantiate()`를 호출할 때마다 메모리가 할당되고, `Destroy()`를 호출할 때마다 GC가 발생합니다. 자주 생성하고 파괴하는 오브젝트는 풀링하세요: 발사체, 파티클, 적, 픽업 아이템, 오디오 소스 등.

## Unity 내장 ObjectPool<T> (2021+)

```csharp
using UnityEngine.Pool;

public sealed class ProjectilePool : MonoBehaviour
{
    [SerializeField] private Projectile m_refPrefab;
    [SerializeField] private int m_iDefaultCapacity = 20;
    [SerializeField] private int m_iMaxSize = 100;

    private ObjectPool<Projectile> m_pool;

    private void Awake()
    {
        m_pool = new ObjectPool<Projectile>(
            createFunc: CreateProjectile,
            actionOnGet: OnGetProjectile,
            actionOnRelease: OnReleaseProjectile,
            actionOnDestroy: OnDestroyProjectile,
            collectionCheck: false,
            defaultCapacity: m_iDefaultCapacity,
            maxSize: m_iMaxSize
        );
    }

    public Projectile Get() => m_pool.Get();

    public void Release(Projectile _refProjectile) => m_pool.Release(_refProjectile);

    private Projectile CreateProjectile()
    {
        Projectile refProjectile = Instantiate(m_refPrefab);
        refProjectile.SetPool(this);
        return refProjectile;
    }

    private void OnGetProjectile(Projectile _refProjectile)
    {
        _refProjectile.gameObject.SetActive(true);
    }

    private void OnReleaseProjectile(Projectile _refProjectile)
    {
        _refProjectile.gameObject.SetActive(false);
    }

    private void OnDestroyProjectile(Projectile _refProjectile)
    {
        Destroy(_refProjectile.gameObject);
    }
}

// Projectile은 스스로를 풀에 반납한다
public sealed class Projectile : MonoBehaviour
{
    private ProjectilePool m_refPool;

    public void SetPool(ProjectilePool _refPool) => m_refPool = _refPool;

    public void ReturnToPool()
    {
        m_refPool.Release(this);
    }
}
```

## 워밍업 (사전 스폰)

런타임 중 끊김을 방지하려면 로딩 시점에 미리 인스턴스화해두세요:

```csharp
private void Start()
{
    // 풀을 미리 워밍업
    List<Projectile> listTemp = new List<Projectile>();
    for (int i = 0; i < m_iDefaultCapacity; i++)
    {
        listTemp.Add(m_pool.Get());
    }
    for (int i = 0; i < listTemp.Count; i++)
    {
        m_pool.Release(listTemp[i]);
    }
    listTemp.Clear();
}
```

## 풀 반납 생명주기

핵심 규칙: **오브젝트는 풀에 반납될 때 반드시 자신의 상태를 초기화해야 합니다.**

```csharp
private void OnReleaseProjectile(Projectile _refProjectile)
{
    // 상태 초기화
    _refProjectile.transform.position = Vector3.zero;
    _refProjectile.transform.rotation = Quaternion.identity;
    _refProjectile.ResetState(); // 속도, 데미지 플래그, 타이머 초기화

    // 비활성화
    _refProjectile.gameObject.SetActive(false);
}
```

## 언제 풀링해야 하는가

**풀링해야 하는 것:**
- 발사체 (총알, 화살, 스펠)
- 파티클 이펙트
- 오디오 소스 (원샷 사운드)
- 웨이브 기반 게임의 적
- 픽업 아이템
- 데미지 숫자 / 플로팅 텍스트
- 트레일 렌더러

**풀링하지 말아야 하는 것:**
- 일회성 오브젝트 (보스, 고유 NPC)
- 한 번만 생성되는 작은 오브젝트 (데이터 컨테이너)
- 씬 전체 동안 살아있는 오브젝트

## 풀 크기 설정

- **작게 시작하세요** — 대부분의 풀은 인스턴스 10~20개면 충분합니다
- **모니터링하세요** — 게임플레이 중 Profiler에서 `Instantiate`가 보이면 풀 크기를 늘리세요
- **최대치를 설정하세요** — 무한정 커지는 것을 막기 위해 `maxSize`를 설정하세요 (예: 100~200)
- **레벨별로 튜닝하세요** — 레벨마다 필요한 풀 크기가 다를 수 있습니다

## 범용 풀 매니저

```csharp
public sealed class PoolManager : MonoBehaviour
{
    private readonly Dictionary<GameObject, ObjectPool<GameObject>> m_hashPools = new();

    public GameObject Get(GameObject _refPrefab, Vector3 _vPosition, Quaternion _qRotation)
    {
        if (!m_hashPools.ContainsKey(_refPrefab))
        {
            m_hashPools[_refPrefab] = new ObjectPool<GameObject>(
                () => Instantiate(_refPrefab),
                _refObj => _refObj.SetActive(true),
                _refObj => _refObj.SetActive(false),
                _refObj => Destroy(_refObj),
                false, 10, 100
            );
        }

        GameObject refObj = m_hashPools[_refPrefab].Get();
        refObj.transform.SetPositionAndRotation(_vPosition, _qRotation);
        return refObj;
    }

    public void Release(GameObject _refPrefab, GameObject _refInstance)
    {
        m_hashPools[_refPrefab].Release(_refInstance);
    }
}
```

## WaitForSeconds 캐싱

`WaitForSeconds`도 풀링(캐싱)하는 것을 잊지 마세요:

```csharp
// 나쁜 예 — 매번 할당됨
yield return new WaitForSeconds(0.5f);

// 좋은 예 — 캐싱해서 재사용
private readonly WaitForSeconds m_waitHalfSecond = new WaitForSeconds(0.5f);
yield return m_waitHalfSecond;
```
