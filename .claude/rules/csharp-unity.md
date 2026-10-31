# C# 스타일 — 이 프로젝트 컨벤션

## 1. 위치별 접두사

| 위치 | 규칙 | 예 |
|---|---|---|
| 멤버 필드 | `m_` + 타입 접두사 | `m_fMoveSpeed`, `m_refRigidbody`, `m_SOAttackInfo` |
| 매개변수 | `_` + 타입 접두사 | `_fSpeed`, `_refBB`, `_eTier`, `_tToken` |
| 로컬 변수 | 타입 접두사만 | `vLookDir`, `qRot`, `refObj`, `iLast` |
| 데이터 컨테이너의 public 필드 | 접두사 없는 PascalCase (§4) | `Damage`, `PoolPrefab`, `TargetTr` |
| const | `SCREAMING_SNAKE` | `GOLDEN_ANGLE_DEG` |
| static readonly 해시/ID | PascalCase | `JumpHash`, `ColorId` |
| 클래스 · 메서드 · 프로퍼티 | PascalCase, 프로퍼티는 접두사 없음 | `WeaponType`, `FireTransform` |
| 싱글톤 | `m_Instance` — 선언 형태는 `architecture.md` | |

## 2. 타입 접두사

| 접두사 | 타입 | 예 |
|---|---|---|
| `f` `i` `l` `d` `b` | float, int, long, double, bool | `m_fCooldown`, `_iValue`, `lDamage`, `m_bLookTarget` |
| `v` | Vector2 / Vector3 | `m_vOffset`, `_vTargetPos` |
| `q` | Quaternion | `qRot`, `_qBase` |
| `str` | string | `strPath` |
| `e` | enum 값 | `m_eWeaponType`, `_eState` |
| `t` | struct 값 — 직접 만든 `t*` 와 Unity/시스템 struct (`Color`, `LayerMask`, `Rect`, `Bounds`, `Ray`, `JobHandle`, `CancellationToken`) | `m_tShotInfo`, `m_tHitLayer`, `m_tHandle`, `_tToken` |
| `ref` | 클래스 참조 전반 — 컴포넌트, Transform, 일반 클래스, Tween/Sequence | `m_refFireTr`, `refSeq`, `_refBB` |
| `SO` | 이 클래스가 읽어서 자기 상태를 만드는 데이터 본체 SO | `m_SOAttackInfo`, `_SOData` |
| `list` | `List<T>`, `NativeList<T>` | `m_listWeapon`, `m_listSpeed` |
| `arr` | 배열, `NativeArray<T>` | `m_arrSfxSource`, `m_arrCenter` |
| `hash` | `Dictionary<K,V>`, `HashSet<T>` | `m_hashPool` |
| `que` | `Queue<T>` | `m_queResult` |
| `PQ` | `PriorityQueue<T>` | `m_PQTimer` |
| `subject` / `rp` | R3 `Subject<T>` / `ReactiveProperty<T>` | `m_subjectDied`, `m_rpExp` |
| `disposable` / `bag` | `IDisposable` 구독 핸들 / `DisposableBag` | `m_disposableHit`, `m_bagEvents` |
| `cts` | `CancellationTokenSource` | `m_cts`, `m_ctsNockback` |

- **Transform 변수 이름은 `Tr`로 끝낸다**: `m_refFireTr`, `TargetTr`, `refOwnerTr`
- `TransformAccessArray` 는 `m_transformArray`
- CancellationToken 매개변수는 `_tToken` (`_token`, `_ct` 아님)

```csharp
// SO 참조는 두 갈래
[SerializeField] private SOAttackInfo m_SOAttackInfo;     // 이 클래스가 읽는 데이터 본체
[SerializeField] private SOPoolData m_refBulletPoolData;  // 남을 찾아가는 열쇠 (풀 키, BT 루트, 선행 카드)

// ReactiveProperty 는 백킹과 노출을 분리
private readonly ReactiveProperty<int> m_rpExp = new ReactiveProperty<int>(0);
public ReadOnlyReactiveProperty<int> Exp => m_rpExp;
```

## 3. 타입 이름

| 종류 | 패턴 | 예 |
|---|---|---|
| enum | `e*` | `eWeaponType`, `eNodeState` |
| struct | `t*` | `tShotInfo`, `tSpawnData` |
| ScriptableObject | `SO*` | `SOAttackInfo`, `SOStrafeNode` |
| Job struct | `*Job` (`t` 안 붙임) | `MoveJob`, `GridOverlapJob` |
| interface | `I*` | `IPoolable`, `IAttackObject` |

SO 생성 메뉴는 `[CreateAssetMenu(fileName = "SO_<이름>", menuName = "Game/<분류>/<이름>")]` — 예: `menuName = "Game/Monster/ActionNode/StrafeNode"`, `"Game/Load/PoolData"`

## 4. 필드는 두 종류 — 데이터 컨테이너 vs 행동 클래스

**데이터 컨테이너** — 데이터 SO, `[Serializable]` 데이터 class/struct, Job struct:
`public` PascalCase 필드, 접두사 없음, `[Header]`로 묶는다. 로직은 변환 메서드(`MakeAttackInfo()`) 정도만.
예: `SOAttackInfo`, `SOPoolData`, `SOStage`, `SOObjectInfo`, `BlackBoard`, `AttackInfo`, `tShotInfo`

```csharp
[CreateAssetMenu(fileName = "SO_Stage", menuName = "Game/Dungeon/Stage")]
public sealed class SOStage : ScriptableObject
{
    [Header("Boss")]
    public SOPoolData BossPrefab;
    public Vector3 BossSpawnPosition;
    public float BossShowDistance;
}
```

**행동 클래스** — MonoBehaviour, 로직을 가진 SO(BT 노드, Feature, BulletAction):
`[SerializeField] private m_*` + 외부에서 읽는 것만 프로퍼티 (§5).

## 5. 캡슐화 — 최소 가시성 (행동 클래스)

- 필드·메서드·프로퍼티·중첩 타입은 기본 `private`. 다른 클래스가 **실제로** 호출/읽기/쓰기 할 때만 연다
- 테스트: non-private 으로 만들기 전에 현재 코드베이스의 호출자를 특정한다. 못 하면 `private`
- `[SerializeField]`는 인스펙터에서 실제로 조정하는 값·참조에만. 런타임 상태, 내부 플래그, 캐싱된 참조는 그냥 `private`

```csharp
[SerializeField] private float m_fMoveSpeed = 5.0f;    // 인스펙터에서 조정
[SerializeField] private Transform m_refFireTr = null; // 인스펙터 참조
public Transform FireTransform => m_refFireTr;         // Drone 이 읽음
private int m_iCurrentHP;                              // 런타임 상태 — 직렬화 안 함
```

## 6. 파일 구조

### 헤더 블록
using 아래, 어트리뷰트 위. 제목은 가운데, 본문은 **`기능 :`**. 이 타입이 무엇을/왜 담당하는지만 쓴다 — "Update 없음", "할당 없음" 같은 부가 설명은 넣지 않는다.

```csharp
using UnityEngine;

/*///////////////////////////////////////////
            SOStrafeNode
기능 : 일정 시간마다 방향을 반전하며 횡이동
       타이머/방향은 BlackBoard에 저장 (SO 데이터 오염 방지)
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_StrafeNode", menuName = "Game/Monster/ActionNode/StrafeNode")]
public sealed class SOStrafeNode : SONode
```

### 한 파일에 여러 타입
파일 이름 = 주 클래스 이름 (MonoBehaviour/ScriptableObject 는 Unity 요구사항). 그 클래스와 붙어 다니는 enum · struct · `[Serializable]` 데이터 클래스는 **같은 파일**에 두고 각자 헤더를 단다.
예: `BehaviorTree.cs` = `eNodeState` + `SONode` + `SOListNode` + `BlackBoard` + `BehaviorTree`, `SOAttackInfo.cs` = `SOAttackInfo` + `AttackInfo` + `tShotInfo`.
여러 시스템이 따로 쓰는 독립 개념이 되면 그때 파일을 나눈다.

### sealed
새로 만드는 타입은 기본 `sealed` — 상속을 의도해 설계한 베이스(`SONode`, `Bullet`, `RandomFeatureCard`)만 푼다. 기존 파일의 `public class` 선언은 그 작업 범위가 아니면 바꾸지 않는다.

### 멤버 순서
- 필드는 **기능 묶음(`[Header]`) 단위**로 놓는다. 한 묶음 안에 직렬화/비직렬화 필드가 섞여도 된다
- 프로퍼티는 **백킹 필드 바로 아래** (`m_refFireTr` 다음 줄에 `FireTransform`)
- 메서드는 Unity 생명주기 → 진입점 메서드 → 그 메서드가 부르는 private 헬퍼를 바로 아래. public/private 끼리 몰아서 정렬하지 않는다

## 7. 제어 흐름 · 포맷

- 중괄호는 항상 다음 줄 (Allman)
- 한 줄짜리 `if` / `else` / `for` 는 중괄호 없이, **본문을 다음 줄**에 들여쓴다. 조건과 같은 줄에 붙이지 않는다
- 블록이 필요하면 여러 줄로 연다 — `{ Destroy(gameObject); return; }` 같은 한 줄 블록 금지
- bool 은 **`== true` / `== false`로 명시**한다. `!`는 메서드 호출 결과(`TryGetValue`, `ContainsKey`)에만 쓴다
- `for` 증가는 **`++i`** (전위). 핫 패스(`Update`, `FixedUpdate`)에서는 `foreach` 대신 인덱스 `for`
- 모든 멤버에 명시적 접근 제한자 — 암묵적 `private` 금지
- `var`는 오른쪽에서 타입이 보일 때만
- 매직 스트링 금지 — `nameof()`, `Animator.StringToHash()`, `Shader.PropertyToID()`
- 게임플레이 코드에서 LINQ 금지, 문자열 조합은 `StringBuilder`, tag 대신 layermask

```csharp
for (int i = 0; i < m_listWeapon.Count; ++i)
{
    if (m_listWeapon[i].gameObject.activeSelf == false)
        continue;

    if (m_listWeapon[i].CheckTime() == true)
        m_listWeapon[i].Fire(vTargetPos, m_refTargetScnner.Target);
}

if (m_bHorizontaol == true)
    _refBB.StrafeDir = _refBB.Owner.transform.right;
else
    _refBB.StrafeDir = _refBB.Owner.transform.up;
```

## 8. 주석 · 어트리뷰트

- 주석은 한국어. 헤더 블록은 항상 쓰고, 그 밖에는 코드만 봐서는 안 보이는 **"왜"**만 남긴다
- `[Header("English")]`, `[Tooltip("한국어 설명")]`
