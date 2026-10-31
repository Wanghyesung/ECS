---
name: jobs-burst
description: "대량 오브젝트 이동을 Job(Burst) + TransformAccessArray 로 일괄 처리하는 MoveManager 패턴 — 영구 등록 + 활성 플래그, Update 에서 Schedule / LateUpdate 에서 Complete, NativeList 수명과 Dispose 가드, 실행 순서, Job struct 규칙. 탄·미사일처럼 수백~수천 개가 매 프레임 움직이는 코드를 쓸 때 사용합니다."
globs: ["**/*MoveManager*.cs", "**/*Job*.cs"]
---

# Jobs + Burst — MoveManager 패턴

같은 규칙으로 매 프레임 움직이는 오브젝트가 수백 개 이상이면(탄, 미사일, 유도탄) 오브젝트마다 `Update`를 두지 않고 매니저 하나가 Job 으로 일괄 처리한다. 예: `BulletMoveManager`, `MissileMoveManager`, `GuidedMoveManager`.

## 흐름

1. **영구 등록** — 풀 오브젝트 `Awake`에서 `RegisterPermanent(transform)` 1회, 받은 인덱스를 들고 있는다
2. **발사(Pop)** — `Activate(iIndex, fSpeed)`: 값만 덮어쓰고 활성 플래그 on
3. **반납(OnDisable)** — `Deactivate(iIndex)`: 플래그 off. 배열에서 빼지 않는다 (TransformAccessArray 재구성 비용 회피)
4. **`Update`에서 Schedule, `LateUpdate`에서 Complete** — 그 사이 다른 스크립트의 Update 동안 워커 스레드가 돈다
5. 도착 콜백처럼 매니지드 객체를 건드리는 결과는 Job 이 결과 배열(`ArrArrived`)에 표시만 하고, Complete 뒤 메인 스레드 루프가 처리한다

## 템플릿

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

/*///////////////////////////////////////////
              BulletMoveManager
기능 : 직선 이동 발사체의 이동을 Job(Burst)으로 일괄 계산한다.
       발사하는 Weapon(0) 뒤, 판정하는 ColliderManager(1000) 앞에서 돈다
 *///////////////////////////////////////////

[DefaultExecutionOrder(500)]
public sealed class BulletMoveManager : MonoBehaviour
{
    public static BulletMoveManager m_Instance = null;

    [SerializeField] private int m_iInitialCapacity = 5120;

    private TransformAccessArray m_transformArray;
    private NativeList<float> m_listSpeed;
    private NativeList<bool> m_listActive;

    private JobHandle m_tHandle;
    private bool m_bScheduled = false;
    private bool m_bDisposed = false;

    private void Awake()
    {
        if (m_Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(gameObject);

        m_transformArray = new TransformAccessArray(m_iInitialCapacity);
        m_listSpeed = new NativeList<float>(m_iInitialCapacity, Allocator.Persistent);
        m_listActive = new NativeList<bool>(m_iInitialCapacity, Allocator.Persistent);
    }

    public int RegisterPermanent(Transform _refTr)
    {
        int iIndex = m_listSpeed.Length;
        m_transformArray.Add(_refTr);
        m_listSpeed.Add(0.0f);
        m_listActive.Add(false);
        return iIndex;
    }

    public void Activate(int _iIndex, float _fSpeed)
    {
        if (m_bDisposed == true)
            return;

        m_listSpeed[_iIndex] = _fSpeed;
        m_listActive[_iIndex] = true;
    }

    // 씬 종료 때 다른 오브젝트의 OnDisable 이 이 매니저의 OnDestroy(Dispose) 뒤에 불릴 수 있다
    public void Deactivate(int _iIndex)
    {
        if (m_bDisposed == true)
            return;

        m_listActive[_iIndex] = false;
    }

    private void Update()
    {
        if (m_listSpeed.Length == 0)
            return;

        var tJob = new MoveJob
        {
            ArrSpeed = m_listSpeed.AsArray(),
            ArrActive = m_listActive.AsArray(),
            FDeltaTime = Time.deltaTime
        };

        m_tHandle = tJob.Schedule(m_transformArray);
        m_bScheduled = true;
    }

    private void LateUpdate()
    {
        if (m_bScheduled == false)
            return;

        m_tHandle.Complete();
        m_bScheduled = false;
    }

    private void OnDestroy()
    {
        m_bDisposed = true;

        if (m_bScheduled == true)
            m_tHandle.Complete();

        if (m_transformArray.isCreated)
            m_transformArray.Dispose();
        if (m_listSpeed.IsCreated)
            m_listSpeed.Dispose();
        if (m_listActive.IsCreated)
            m_listActive.Dispose();
    }

    [BurstCompile]
    private struct MoveJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float> ArrSpeed;
        [ReadOnly] public NativeArray<bool> ArrActive;
        public float FDeltaTime;

        public void Execute(int _iIndex, TransformAccess _tTransform)
        {
            if (ArrActive[_iIndex] == false)
                return;

            Vector3 vForward = _tTransform.rotation * Vector3.forward;
            _tTransform.position += vForward * ArrSpeed[_iIndex] * FDeltaTime;
        }
    }
}
```

## 규칙

- **Job struct**: 이름은 `*Job` (`t` 안 붙임), `[BurstCompile]`, 필드는 public PascalCase (`ArrSpeed`, `FDeltaTime`). class · `List` · `string` 같은 매니지드 타입은 넣을 수 없다
- 읽기만 하는 배열은 `[ReadOnly]`
- `NativeList`는 `.AsArray()`로 넘긴다. **Schedule 된 동안 `Add` 금지** (길이가 바뀌면 경합) — 그래서 등록은 `Awake` 1회, 발사 때는 값만 덮어쓴다
- `Allocator.Persistent`로 만든 것은 `OnDestroy`에서 진행 중 Job 을 Complete 한 뒤 `IsCreated` 확인 후 Dispose
- **실행 순서**: 등록·활성화하는 쪽(기본 0) → MoveManager(500) → 판정 `ColliderManager`(1000). 순서가 어긋나면 Schedule 중인 리스트를 건드리거나 판정이 한 프레임 늦는다
- Burst 안의 수학은 `Unity.Mathematics`(`math`, `float3`, `quaternion`)를 쓴다 — `MissileMoveManager`의 `quaternion.LookRotationSafe`
- 프리팹에 Rigidbody/Collider 가 남아 있으면 Job 으로 옮겨도 `Physics.SyncColliderTransform` 비용이 그대로 남는다 → [[collider-system]]
- 디버그 카운터·`Debug.Log`를 `Activate`/`Deactivate` 같은 고빈도 경로에 남기지 않는다
