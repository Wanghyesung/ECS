# ECS 프로젝트 아키텍처 문서

> Unity 6 URP 기반 3D 우주선 슈팅 게임  
> 2D 종스크롤/횡스크롤 슈팅의 조작감을 3D로 구현

---

## 목차

1. [전체 구조 한눈에 보기](#1-전체-구조-한눈에-보기)
2. [시스템별 상세 설명](#2-시스템별-상세-설명)
   - [Player System](#21-player-system)
   - [Monster & Behavior Tree System](#22-monster--behavior-tree-system)
   - [Object Pool System](#23-object-pool-system)
   - [Weapon & Attack System](#24-weapon--attack-system)
   - [Input System](#25-input-system)
   - [Animation System](#26-animation-system)
   - [Effect System](#27-effect-system)
3. [Behavior Tree 노드 카탈로그](#3-behavior-tree-노드-카탈로그)
4. [데이터 흐름 — 몬스터가 발사하기까지](#4-데이터-흐름--몬스터가-발사하기까지)
5. [데이터 흐름 — 플레이어가 발사하기까지](#5-데이터-흐름--플레이어가-발사하기까지)
6. [신입 개발자 가이드 — 새 기능 추가하기](#6-신입-개발자-가이드--새-기능-추가하기)

---

## 1. 전체 구조 한눈에 보기

```
┌─────────────────────────────────────────────────────────────┐
│                         Game Scene                          │
│                                                             │
│  ┌──────────────┐          ┌──────────────────────────────┐ │
│  │   Player     │          │         Monster              │ │
│  │  ─────────   │          │  ──────────────────────────  │ │
│  │ PlayerMovement│         │ SOMonsterInfo (SO - 스탯)    │ │
│  │ Aim (조준점) │          │ BlackBoard   (런타임 상태)   │ │
│  │ AnimationTable│         │ BehaviorTree (매 프레임 실행)│ │
│  │ Weapon[]     │          │ Weapon[]     (공격 오브젝트) │ │
│  └──────┬───────┘          └──────────────┬───────────────┘ │
│         │ Fire()                          │ Evaluate()       │
│         ▼                                ▼                   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              Weapon  (공격 실행 단위)                 │   │
│  │  SOAttackInfo (SO - 공격 데이터)                     │   │
│  │  → GetObject(PoolPrefab) → 발사체 소환               │   │
│  └───────────────────────┬──────────────────────────────┘   │
│                           │                                   │
│                           ▼                                   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │             Object Pool                               │   │
│  │  Dictionary<PoolObject, Queue<GameObject>>            │   │
│  │  GetObject / PushObject                               │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌──────────────────┐   ┌──────────────────────────────┐   │
│  │  InputManager    │   │        Bullet / Laser        │   │
│  │  (싱글턴)        │   │  TriggerObject → AttackMonster│   │
│  │  Move / Delta    │   │  HitEffect → 풀 반납          │   │
│  └──────────────────┘   └──────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. 시스템별 상세 설명

---

### 2.1 Player System

> 파일 위치: `Assets/02_Player/`

플레이어는 하나의 `GameObject`에 여러 컴포넌트가 붙는 구조입니다.  
각 컴포넌트가 역할을 분리하며 서로 직접 참조하지 않고 각자 동작합니다.

```
[Player GameObject]
  ├── Player.cs          ← 무기 관리, 발사 판단
  ├── PlayerMovement.cs  ← 이동/회전 처리
  ├── Aim.cs             ← 조준점 위치 계산
  └── AnimationTable.cs  ← 애니메이션 파라미터 제어
```

#### Player.cs
플레이어의 핵심 클래스. 무기 쿨타임을 체크하고 발사를 지시합니다.

```
Q키 입력
  → CheckTime() : 각 Weapon의 쿨타임 체크
  → NeeadNearTarget == true 인 무기가 있으면 FindNearestTarget() 실행
    (Physics.OverlapSphereNonAlloc — GC Alloc 없음)
  → Weapon.Fire(targetPos, nearTargetTr) 호출
```

| 필드 | 설명 |
|------|------|
| `m_listWeapon` | 플레이어가 보유한 Weapon 컴포넌트 목록 |
| `m_listFireWeapon` | 이번 프레임에 발사할 무기만 담는 임시 리스트 (매 프레임 Clear) |
| `m_arrNearCollider` | OverlapSphere 결과 배열 (크기 20, 미리 할당) |

#### PlayerMovement.cs
`InputManager`에서 Move/Delta를 읽어 Rigidbody로 이동/회전합니다.  
- `Update`: 마우스 Delta로 rotation 계산 (X축 -85°~40° 클램프)  
- `FixedUpdate`: MovePosition으로 물리 이동 (Rigidbody 사용)

#### Aim.cs
화면 중앙에서 레이캐스트를 쏘아 조준점의 월드 좌표를 반환합니다.  
`Player.Fire()`가 이 좌표를 `Weapon.Fire()`에 넘깁니다.

#### AnimationTable.cs
상태(`eEntityState`) → Animator 파라미터 매핑 테이블.  
인스펙터에서 `AnimationNode` 리스트를 설정하면 런타임에 Dictionary로 변환되어 빠르게 조회합니다.

```
eEntityState.Move → AnimationNode { ParamName="IsMove", ParamType=Bool }
Player.UpdateOnAnimation(eEntityState.Move, true)
  → AnimationTable.SetBool(Move, true)
  → Animator.SetBool("IsMove", true)
```

---

### 2.2 Monster & Behavior Tree System

> 파일 위치: `Assets/03_Monster/`

몬스터의 AI는 **Behavior Tree(BT)**로 구현되어 있습니다.  
모든 BT 노드는 **Scriptable Object(SO)** 로 만들어져 인스펙터에서 조립합니다.

```
[Monster GameObject]
  ├── Monster.cs        ← IDamageable, SpawnInfo 관리, 상태이상 처리
  ├── BehaviorTree.cs   ← 루트 노드 실행 (매 프레임 Update)
  └── BlackBoard        ← 노드 간 공유 데이터 (Monster.cs 필드)
```

#### Monster.cs

```
Awake: SOMonsterInfo → BlackBoard 초기화 (HP, Speed, State)
Start: FindObjectOfType<Player> → BlackBoard.TargetTr 설정
       SpawnInfo 리스트 → Dictionary<eWeaponType, List<SpawnInfo>> 변환
Update: BehaviorTree.Evaluate(BlackBoard) 호출
```

`IDamageable.TakeDamage()` → 넉백 코루틴 실행  
`StartStateEffect()` → 비트마스크(`CurrentEffects`)로 상태이상 등록  
`CheckStateEffect()` → 만료 여부 확인 후 비트 제거

**상태이상 비트마스크** — 16비트 ushort 하나로 모든 상태이상 관리 (GC Alloc 0)
```
eStatusEffect: Wait=0, Lock=1, Stun=2, Poison=3, Burn=4
CurrentEffects: 0b00000101 → Wait, Stun 동시 활성
```

#### BehaviorTree.cs

매우 단순한 구조입니다. `Update` 마다 루트 SONode의 `Execute(BlackBoard)`를 호출합니다.  
`StopBT()` / `StartBT()`로 일시정지가 가능합니다.

#### BlackBoard

BT 노드들이 공유하는 데이터 컨테이너입니다.  
노드 간에 직접 참조 대신 BlackBoard를 통해 데이터를 주고받습니다.

```csharp
public class BlackBoard
{
    public Monster Owner;           // 이 블랙보드의 주인 몬스터
    public Transform TargetTr;      // 현재 추적 대상 (플레이어)
    public ObjectInfo ObjInfo;      // HP, 상태, 상태이상 배열
    public SpawnInfo CurrentAttackSpawn; // 현재 실행할 공격 정보
    // 추적 파라미터: TraceTime, TraceMinDistance 등
}
```

> **왜 BlackBoard인가?**  
> SO 노드는 에셋이므로 여러 몬스터가 공유합니다.  
> 노드 자체에 상태를 저장하면 몬스터 A의 상태가 몬스터 B에 영향을 줍니다.  
> BlackBoard를 사용하면 노드는 순수 로직만 가지고, 상태는 몬스터 인스턴스별로 분리됩니다.

#### SONode 클래스 계층

```
ScriptableObject
  └── SONode (abstract)              ← Execute(BlackBoard) → eNodeState
        ├── SOListNode (abstract)    ← 자식 노드 List<SONode> 보유 / BT 초기화 시 인스턴스별 클론
        │     ├── SOSequenceNode       ← AND 로직 (하나 실패 → 전체 실패)
        │     ├── SOSelectNode         ← OR 로직 (하나 성공 → 전체 성공)
        │     ├── SOParallelNode       ← 모든 자식 매 프레임 실행 (루트 패시브용)
        │     ├── SOParallelWaitNode   ← 모든 자식 동시 실행 + 완료 대기
        │     └── SORandomSelectNode   ← 자식 중 하나를 랜덤 선택, 타이머 만료 시 재선택
        └── [Action 노드들]          ← 실제 동작 수행 (섹션 3 참고)
```

**eNodeState 반환값 의미**

| 값 | 의미 |
|----|------|
| `Success` | 이번에 할 일을 완료함 |
| `Failure` | 조건 불충족 또는 실패 |
| `Running` | 아직 진행 중 (다음 프레임에 계속) |

---

### 2.3 Object Pool System

> 파일 위치: `Assets/05_Manager/`

자주 생성/삭제되는 오브젝트(총알, 이펙트 등)를 미리 만들어두고 재사용합니다.  
`new` 연산자로 인한 GC Alloc과 Instantiate/Destroy 비용을 없앱니다.

#### 구조

```
ObjectPool (Singleton)
  Dictionary<PoolObject, Queue<GameObject>>
     ↑                      ↑
  프리팹의 PoolObject    실제 인스턴스들의 대기열
  컴포넌트 (키 역할)
```

#### 초기화 흐름 (ObjectPool.Start)

```
PoolInfo 리스트 순회 (인스펙터에서 설정)
  refPrefabPoolObj = 프리팹의 PoolObject 컴포넌트  ← 딕셔너리 키
  m_hashPool.Add(refPrefabPoolObj, new Queue)
  
  iPoolCount 개수만큼:
    instance = Instantiate(prefab)
    instance.GetComponent<PoolObject>().SetOriginalPoolObj(refPrefabPoolObj)
                                        ↑
                          인스턴스가 자신의 원본 키를 기억함
    PushObject(instance)  → 큐에 넣고 비활성화
```

#### GetObject / PushObject

```
GetObject(PoolObject _prefabPoolObj)
  → m_hashPool[_prefabPoolObj].Dequeue()
  → SetActive(true), Pop() 호출
  → GameObject 반환

PushObject(GameObject _instance)
  → instance.GetComponent<PoolObject>().PoolKey  ← SetOriginalPoolObj로 설정된 원본 키
  → m_hashPool[PoolKey].Enqueue(instance)
  → SetActive(false), Push() 호출
```

#### PoolObject.cs

```csharp
public class PoolObject : MonoBehaviour, IPoolable
{
    private PoolObject m_refOriginalPoolObj; // ObjectPool이 주입 (직렬화 안 함)
    public PoolObject PoolKey => m_refOriginalPoolObj;

    [SerializeField] private float m_fAliveTime; // 이 시간 후 자동 반납
    
    // Update: AliveTime 카운트다운 → 0 이하면 PushObject(gameObject)
    // OnPush / OnPop 이벤트로 외부 구독 가능
}
```

**AliveTime 자동 반납 구조**
```
PoolObject.Update() 매 프레임 실행
  m_fAliveTime -= Time.deltaTime
  if (m_fAliveTime <= 0)
    ObjectPool.m_Instance.PushObject(gameObject)
      → 풀로 반납 + SetActive(false)
```

#### 새 풀 오브젝트 추가하는 법

1. 프리팹에 `PoolObject` 컴포넌트 부착
2. `ObjectPool` 인스펙터 → `PoolObject 리스트`에 해당 프리팹 추가, 사전 생성 개수 설정
3. 발사 무기의 `SOAttackInfo.PoolPrefab` 필드에 해당 프리팹 연결

---

### 2.4 Weapon & Attack System

> 파일 위치: `Assets/02_Player/Weapon/`, `Assets/03_Monster/Bullet/`, `Assets/10_Option/`

공격 데이터(SO)와 발사 로직(Weapon), 발사체(Bullet)가 분리된 구조입니다.

```
SOAttackInfo (SO — 불변 설계 데이터)
  → MakeAttackInfo() → AttackInfo (런타임 동적 데이터)

Weapon
  → CheckTime() : 쿨타임 체크
  → Fire() : ObjectPool.GetObject(PoolPrefab) → 발사체 꺼냄
           → SetAttack(AttackInfo) : 발사체에 공격 정보 주입

발사체: Bullet / Missiles / GuidedBullet / Laser
  → TriggerObject.OnHitTargetEnter → AttackMonster()
  → 타겟 데미지 / 이펙트 소환 / PushObject(자기자신) 반납
```

#### SOAttackInfo.cs (Scriptable Object)

무기 하나의 모든 설계 데이터를 보관합니다. 런타임에 변경되지 않습니다.

| 필드 | 설명 |
|------|------|
| `PoolPrefab` | 이 무기가 꺼내 쓸 발사체 프리팹의 PoolObject |
| `WeaponType` | Bullet / Missile / Laser 등 (Monster가 Dictionary 분류에 사용) |
| `Damage` / `Speed` / `CoolDown` | 공격 기본 스탯 |
| `BaseRotationSpeed` / `MaxRotationSpeed` | 호밍 회전 관련 (Missiles 전용) |
| `HitLayers` | 충돌 감지 레이어마스크 |

`MakeAttackInfo()` 호출 시 Speed를 `±SpeedOffset` 범위로 랜덤화하여 AttackInfo 생성.

#### AttackInfo

런타임에 발사체에 넘겨지는 데이터. 타겟 정보나 히트 위치 등 동적 값 포함.

```csharp
public class AttackInfo
{
    public int Damage;
    public float AttackSpeed;   // SOAttackInfo에서 랜덤화된 값
    public float AliveTime;
    public float RotationSpeed; // 호밍 관련
    
    public Vector3 TargetPos;         // 발사 시점의 타겟 좌표
    public Transform TargetTrasnform; // 호밍 타겟 (Missiles 전용)
    public Vector3 HitPosition;       // 피격 지점
    public LayerMask HitLayers;
}
```

#### 발사체 클래스 계층

```
MonoBehaviour
  └── Bullet (기본 직선 발사체)
        ├── Missiles       ← 호밍 미사일 (회전 추적 + 폭발 이펙트)
        └── GuidedBullet   ← 단순 유도탄 (항상 타겟 방향으로 회전)

  └── Laser                ← 판정 영역형 공격 (IAttackObject 직접 구현)
```

**Bullet**: `FixedUpdate`에서 `MovePosition`으로 전진.  
`TriggerObject.OnHitTargetEnter` 이벤트 → `AttackMonster()` → 이펙트 소환 → 자신 반납

**Missiles**: Bullet 상속. `FixedUpdate`에서 타겟 방향으로 점진적 회전 후 전진.  
가속도 공식: `TimeAccel + DistAccel`로 가까울수록 회전 빨라짐.  
Push 이벤트 구독 → Push 시 폭발 이펙트 소환.

**TriggerObject**: `OnTriggerEnter`를 레이어마스크 필터링 후 `OnHitTargetEnter` 이벤트로 래핑.  
`SetTriggerMask()`로 런타임에 레이어 교체 가능.

---

### 2.5 Input System

> 파일 위치: `Assets/06_Input/InputManager.cs`

Unity Input System(`InputActionReference`) 기반 싱글턴.  
`tInputInfo` 구조체를 매 프레임 갱신하여 다른 컴포넌트가 폴링합니다.

```csharp
public struct tInputInfo
{
    public Vector2 MoveDir;   // WASD (normalized)
    public Vector2 ScreenPos; // 마우스 스크린 좌표
    public Vector2 Delta;     // 마우스 이동량
}
```

`PlayerMovement.Update()` → `InputManager.m_Instance.InputInfo.MoveDir` 폴링  
플레이어 발사는 현재 `Player.Update()`에서 `Input.GetKey(KeyCode.Q)` 직접 체크 (추후 InputManager 연동 예정)

---

### 2.6 Animation System

> 파일 위치: `Assets/02_Player/AnimationTable.cs`

`eEntityState` 열거형을 키로 Animator 파라미터를 제어합니다.

```
인스펙터 설정:
  AnimationNode { State=Move, ParamType=Bool, ParamName="IsMove" }
  AnimationNode { State=Dead, ParamType=Trigger, ParamName="Death" }

런타임:
  Awake: List → Dictionary<eEntityState, AnimationNode> 변환 (이후 List 비움)
  SetBool(Move, true) → animator.SetBool("IsMove", true)
```

새 애니메이션을 추가할 때 코드를 건드리지 않고 인스펙터에서 `AnimationNode` 항목만 추가합니다.

---

### 2.7 Effect System

> 파일 위치: `Assets/02_Player/Weapon/HitEffect.cs`, `Assets/Charge.cs`

#### HitEffect.cs

파티클 시스템 기반 이펙트 오브젝트. 풀에서 꺼내면 자동 재생, 완료되면 자동 반납합니다.

```
OnEnable → ParticleSystem.Play()
OnParticleSystemStopped → ObjectPool.m_Instance.PushObject(gameObject)
```

> `PoolObject`의 AliveTime이 아닌 파티클 완료 콜백(`OnParticleSystemStopped`)으로  
> 반납을 처리하여 재생 시간에 관계없이 정확하게 반납됩니다.

#### Charge.cs

몬스터 공격 전 차지 연출용 컴포넌트.

```
SOChargeNode.Execute()
  → Charge.StartCharge(waitTime)
      → ParticleSystem.Play()
      → Update 타이머 카운트다운
      → 완료 시 OnChargeComplete 이벤트 (UnityEvent — 인스펙터에서 연결)
```

---

## 3. Behavior Tree 노드 카탈로그

> 모두 `Assets/03_Monster/BT/`에 위치.  
> `CreateAssetMenu` 어트리뷰트로 인스펙터에서 직접 에셋 생성 가능.

### 복합 노드 (자식 노드를 가짐)

| 클래스 | 동작 | 용도 |
|--------|------|------|
| `SOSequenceNode` | 자식을 순서대로 실행. 하나라도 Failure → 전체 Failure | 조건 체크 → 행동 순서 구성 |
| `SOSelectNode` | 자식을 순서대로 실행. 하나라도 Success → 전체 Success | 여러 행동 중 가능한 것 선택 |
| `SOParallelNode` | 모든 자식을 매 프레임 실행 (결과 무관) | 루트에 배치, 패시브 효과와 메인 BT 병행 |
| `SOParallelWaitNode` | 모든 자식 동시 실행. 완료된 자식은 대기, 전부 Success 시 종료 | 차지(Charge) + 방향 조준(LookAt) 병행 |
| `SORandomSelectNode` | 자식 중 하나를 랜덤 선택해 실행. 타이머(`MinDuration`~`MaxDuration`) 만료 시 새 자식 재선택. 자식이 Failure 반환 시 즉시 재선택 | 몬스터 이동 패턴 랜덤화 |

> **SOListNode 클론 규칙**: `SOListNode` 계열은 `BehaviorTree.Awake()`에서 `CloneChildren()`으로 인스턴스별 복사본을 생성합니다. 덕분에 `SOSelectNode`의 `iCurrentIdx`, `SORandomSelectNode`의 `m_iCurrentIdx`·`m_fTimer` 등 런타임 상태를 노드 필드에 직접 저장해도 SO 데이터 오염이 없습니다. 반면 **단순 `SONode`(클론 안 됨)** 는 여전히 상태를 BlackBoard에 저장해야 합니다.

### 조건 노드 (체크만 함)

| 클래스 | 체크 내용 | 결과 |
|--------|-----------|------|
| `SOCheckIdleStateNode` | `BlackBoard.ObjInfo.State == Idle` | Success / Failure |
| `SOCheckPOVNode` | 시야각 내 타겟 존재 여부 (현재 비활성) | 항상 Success |
| `SOCheckAttackTimeNode` | 지정 무기 쿨타임 완료 여부 | Success 시 `CurrentAttackSpawn` 설정 |

### 행동 노드 (실제 동작 수행)

| 클래스 | 동작 | 반환 |
|--------|------|------|
| `SOTraceNode` | 타겟 방향으로 이동 | 사거리 내 도달 시 Success, 이동 중 Running |
| `SOLookAtTargetNode` | 타겟 방향으로 회전 | 임계각 이내 시 Success, 회전 중 Running |
| `SOChargeNode` | 차지 파티클 실행 + Wait 상태이상 적용 | 즉시 Success |
| `SOWaitStatusEffectNode` | 지정 상태이상이 해제될 때까지 대기 | 해제 시 Success, 대기 중 Running |
| `SOFireObjectNode` | 타겟을 향해 발사 (풀 여유 체크) | Success / Failure |
| `SOFireRadialDirNode` | 피보나치 구 분포로 방사형 전방향 발사 | Success / Failure |
| `SOPassiveEffectNode` | 상태이상 DoT 처리 (매 프레임, GC 0) | 항상 Success |

---

## 4. 데이터 흐름 — 몬스터가 발사하기까지

전형적인 보스 공격 패턴 (`Sequence → CheckAttackTime → Charge → WaitStatus → LookAt → Fire`)을 예로 설명합니다.

```
1. Monster.Update()
   └─ BehaviorTree.Evaluate(BlackBoard)
       └─ 루트 SOParallelNode.Execute(BB)
            ├─ SOPassiveEffectNode  : DoT 처리 (항상 Success)
            └─ SOSequenceNode       : 공격 시퀀스 시작

2. SOSequenceNode 자식 순서대로 실행:

   [1] SOCheckIdleStateNode
       BB.ObjInfo.State == Idle?  → Success 시 다음으로

   [2] SOCheckAttackTimeNode (eWeaponType.Missile)
       Monster.HashSpawn[Missile] 리스트에서 쿨타임 완료 Weapon 탐색
       → BB.CurrentAttackSpawn = 해당 SpawnInfo
       → Success

   [3] SOParallelWaitNode (Charge + LookAt 병행)
       ├─ SOChargeNode
       │    SpawnInfo.ChargeParticle.GetComponent<Charge>().StartCharge(waitTime)
       │    Monster.StartStateEffect(Wait, waitTime)  → 비트마스크 등록
       │    → 즉시 Success (이후 대기 안 함)
       │
       └─ SOWaitStatusEffectNode(Wait)
            Monster.CheckStateEffect(Wait)  → 아직 활성 → Running
            ... (waitTime 초 후) ...
            → EndTime 초과 → 비트 제거 → Success
       
       두 자식 모두 Success → SOParallelWaitNode Success

   [4] SOLookAtTargetNode
       Quaternion.RotateTowards(현재방향, 타겟방향, speed * dt)
       임계각 이내 → Success (그전까지 Running)

   [5] SOFireObjectNode
       ObjectPool.GetObjectCount(Weapon.FireBulletPrefab) >= SpawnCount?
       → Weapon.Fire(BB.TargetTr.position, BB.TargetTr)
            → ObjectPool.GetObject(SOAttackInfo.PoolPrefab)
            → bullet.SetAttack(AttackInfo)
       → Success

3. Bullet 이동 및 충돌
   FixedUpdate: Rigidbody.MovePosition(forward * speed * dt)
   TriggerObject.OnTriggerEnter → 레이어 필터링 → OnHitTargetEnter 이벤트
   Bullet.AttackMonster(collider)
     → IDamageable.TakeDamage(AttackInfo)
     → HitEffect 소환 (GetObject)
     → ObjectPool.PushObject(자신)  ← 반납
```

---

## 5. 데이터 흐름 — 플레이어가 발사하기까지

```
1. InputManager.Update() → tInputInfo 갱신

2. Player.Update()
   Q키 누름 → Fire()
     ① Weapon.CheckTime() → 쿨타임 지난 Weapon만 m_listFireWeapon에 추가
     ② NeeadNearTarget 있으면 FindNearestTarget()
          Physics.OverlapSphereNonAlloc (pre-allocated 배열)
          → m_refNearTargetTr 설정
     ③ Weapon.Fire(Aim.TargetPosition, m_refNearTargetTr)
          → ObjectPool.GetObject(SOAttackInfo.PoolPrefab)
          → bullet.transform.position = FirePoint 위치
          → bullet.LookAt(targetPos)
          → bullet.SetAttack(m_refAttackInfo)

3. Aim.Update()
   Screen 중앙 → Ray → Raycast
   → m_tTargetPosition (월드 좌표)
   히트 시 조준 UI 빨간색, 미스 시 흰색
```

---

## 6. 신입 개발자 가이드 — 새 기능 추가하기

### 새 발사체 타입 추가

1. **프리팹 생성**: `Bullet` 또는 `Laser`를 상속하는 스크립트 작성
2. **PoolObject 부착**: 프리팹에 `PoolObject` 컴포넌트 추가, `AliveTime` 설정
3. **ObjectPool 등록**: ObjectPool 인스펙터의 `PoolObject 리스트`에 프리팹 추가
4. **SOAttackInfo 생성**: `Assets > Create > Game > Attack Info`로 에셋 생성  
   `PoolPrefab` 필드에 위 프리팹 연결, 스탯 설정
5. **Weapon에 연결**: 사용할 Weapon 컴포넌트의 `SOAttackInfo` 필드에 위 에셋 연결

### 새 BT 노드 추가

```csharp
[CreateAssetMenu(fileName = "SO_MyNode", menuName = "Game/Monster/ActionNode/MyNode")]
public class SOMyNode : SONode
{
    [SerializeField] private float m_fMyParam;  // 인스펙터 노출 파라미터
    
    public override eNodeState Execute(BlackBoard _refBB)
    {
        // 중요: SO는 여러 몬스터가 공유하므로 상태를 SO에 저장하면 안 됨
        // 모든 상태는 _refBB (BlackBoard) 에 저장할 것
        
        if (조건 불충족)
            return eNodeState.Failure;
        
        if (아직 진행 중)
            return eNodeState.Running;
        
        return eNodeState.Success;
    }
}
```

에셋 생성 후 `SOSequenceNode` 또는 `SOSelectNode`의 `listNode`에 드래그하여 연결.

### 새 상태이상 추가

```csharp
// 1. Player.cs / Monster.cs 공통 열거형에 추가
public enum eStatusEffect { Wait, Lock, Stun, Poison, Burn, Freeze, End } // Freeze 추가

// 2. Monster.StartStateEffect(eStatusEffect.Freeze, duration) 호출
// 3. SOPassiveEffectNode가 자동으로 DoT 처리 (TickDamage > 0인 경우)
// 4. SOWaitStatusEffectNode에 Freeze 설정 → 해제될 때까지 BT 대기 가능
```

### 코딩 컨벤션 요약

| 대상 | 규칙 | 예시 |
|------|------|------|
| 멤버 변수 | `m_` 접두사 + 타입 접두사 | `m_fMoveSpeed`, `m_listWeapon` |
| 매개변수 | `_` 접두사 + 타입 접두사 | `_fSpeed`, `_refTarget` |
| 클래스/메서드 | PascalCase | `PlayerMovement`, `SetAttack` |
| 타입 접두사 | f(float), i(int), v(Vector), list(List), hash(Dictionary), str(string) | |

**성능 규칙**:
- `Update` 안에서 `new` 금지 (GC Alloc 0 목표)
- 자주 생성/삭제하는 오브젝트는 반드시 ObjectPool 사용
- `GetComponent`는 `Awake`에서 캐싱, 매 프레임 호출 금지
- SO 데이터는 읽기 전용 취급 — 런타임 상태는 BlackBoard 또는 컴포넌트 인스턴스에 저장
