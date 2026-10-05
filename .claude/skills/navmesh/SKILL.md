---
name: navmesh
description: "Unity 내비게이션 — NavMeshAgent 설정, NavMeshSurface 베이크, 오프메시 링크, 동적 장애물, 경로 상태 확인, 순찰 패턴."
globs: ["**/*Nav*.cs", "**/*Pathfind*.cs", "**/*Agent*.cs"]
---

# NavMesh 내비게이션

## 설정

1. 환경 부모 오브젝트에 `NavMeshSurface`를 붙이고 Bake
2. 움직이는 캐릭터에 `NavMeshAgent`

## NavMeshAgent

```csharp
private NavMeshAgent m_refAgent;

private void Awake()
{
    m_refAgent = GetComponent<NavMeshAgent>();
    m_refAgent.speed = 3.5f;
    m_refAgent.acceleration = 8.0f;
    m_refAgent.angularSpeed = 120.0f;
    m_refAgent.stoppingDistance = 0.5f;
    m_refAgent.autoBraking = true;
}

public void MoveTo(Vector3 _vDestination)
{
    m_refAgent.SetDestination(_vDestination);
}
```

## 경로 상태 · 도착 판정

```csharp
private void Update()
{
    if (m_refAgent.pathPending == true)
        return;

    if (m_refAgent.pathStatus == NavMeshPathStatus.PathInvalid)
        return;   // 갈 수 없는 목적지

    if (m_refAgent.remainingDistance <= m_refAgent.stoppingDistance)
        Arrive();
}
```

`PathPartial`은 장애물 때문에 중간까지만 갈 수 있다는 뜻이다.

## 순찰

```csharp
public sealed class PatrolBehavior : MonoBehaviour
{
    [SerializeField] private Transform[] m_arrWaypointTr;
    [SerializeField] private float m_fWaitTime = 2.0f;

    private NavMeshAgent m_refAgent;
    private int m_iCurrentWaypoint;
    private float m_fWaitTimer;

    private void Awake()
    {
        m_refAgent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (m_refAgent.pathPending == true)
            return;

        if (m_refAgent.remainingDistance > m_refAgent.stoppingDistance)
            return;

        m_fWaitTimer -= Time.deltaTime;
        if (m_fWaitTimer > 0.0f)
            return;

        m_iCurrentWaypoint = (m_iCurrentWaypoint + 1) % m_arrWaypointTr.Length;
        m_refAgent.SetDestination(m_arrWaypointTr[m_iCurrentWaypoint].position);
        m_fWaitTimer = m_fWaitTime;
    }
}
```

## NavMeshObstacle

- **Carve:** NavMesh 에 구멍을 낸다 (비쌈 — 정적이거나 가끔 움직이는 장애물)
- **Block:** NavMesh 를 바꾸지 않고 에이전트가 돌아간다 (쌈 — 움직이는 장애물)

## 오프메시 링크

점프, 사다리, 텔레포터처럼 끊긴 영역을 잇는다.
- 자동: NavMeshSurface 의 Jump Distance / Drop Height
- 수동: 두 지점 사이에 `NavMeshLink`

## 런타임 재베이크

```csharp
m_refNavMeshSurface.BuildNavMesh();                                   // 전체 재베이크
m_refNavMeshSurface.UpdateNavMesh(m_refNavMeshSurface.navMeshData);   // 갱신만
```

## 영역 비용

- Navigation 설정에서 영역(Walkable, Water, Road) 정의
- 비용이 높을수록 에이전트가 피한다. 에이전트별로 `m_refAgent.SetAreaCost(iAreaIndex, fCost)`
