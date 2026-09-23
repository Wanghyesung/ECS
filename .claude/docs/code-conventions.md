# ECS 코드 컨벤션

---

## 1. 위치별 접두사

| 위치 | 규칙 | 예 |
|---|---|---|
| 멤버 필드 | `m_` + 타입 | `m_fMoveSpeed`, `m_refRigidbody` |
| 매개변수 | `_` + 타입 | `_fSpeed`, `_refBB`, `_eTier` |
| 로컬 변수 | 타입만 | `vTargetPos`, `iLimit`, `refObj` |
| const | `SCREAMING_SNAKE` | `MAX_HIT_EFFECT_PER_FRAME` |
| static readonly 해시 | PascalCase | `JumpHash`, `TargetWorldPosId` |
| 클래스 · 메서드 | PascalCase | `PlayerController`, `SetAttack` |
| public 프로퍼티 | 접두사 없음 | `AttackInfo`, `CurrentHP`, `FireTransform` |

---

## 2. 타입 접두사

| 접두사 | 타입 | 예 |
|---|---|---|
| `ref` | 참조형 전반 — 컴포넌트, Transform, 클래스 | `m_refRigidbody`, `_refBB`, `refObj` |
| `f` | float | `m_fMoveSpeed`, `_fSpeed`, `fRatio` |
| `i` | int | `m_iBulletCount`, `_iAmount`, `iLimit` |
| `v` | Vector2 / Vector3 | `m_vOffset`, `_vDir`, `vTargetPos` |
| `b` | bool | `m_bJudging`, `_bStopEffect`, `bCanApply` |
| `q` | Quaternion | `qRot`, `qTargetRot`, `qJitter` |
| `t` | struct 값 | `m_tShotInfo`, `_tContext`, `tRay` |
| `e` | enum 값 | `m_eWeaponType`, `_eTier`, `eState` |
| `list` | `List<T>` | `m_listWeapon`, `_listHitActions` |
| `arr` | 배열 · NativeArray | `m_arrWeaponDefaultActive` |
| `hash` | `Dictionary<K,V>` | `m_hashPool`, `m_hashSpawn` |
| `que` | `Queue<T>` | `m_queResult` |
| `str` | string | `_strPath`, `strPath` |
| `d` | double | `m_dElapsed` |
| `SO` | 이 클래스의 데이터 본체 SO | `m_SOAttackInfo`, `m_SOMonsterInfo` |
| `subject` | R3 `Subject<T>` | `m_subjectDied`, `m_subjectMoveButton` |
| `rp` | R3 `ReactiveProperty<T>` | `m_rpExp`, `m_rpLevel` |
| `disposable` | IDisposable 구독 핸들 | `m_disposableDied` |
| `cts` | CancellationTokenSource | `m_cts`, `m_ctsNockback` |

### 타입 이름 자체

`enum`은 `e*`, `struct`는 `t*`, ScriptableObject는 `SO*`. 변수 접두사와 같은 글자를 써서
`eWeaponType m_eWeaponType`처럼 타입과 변수가 맞물린다.

| 종류 | 패턴 | 예 |
|---|---|---|
| enum | `e*` | `eWeaponType`, `eNodeState`, `eEntityState` |
| struct | `t*` | `tShotInfo`, `tInputInfo`, `tColliderPair` |
| ScriptableObject | `SO*` | `SOAttackInfo`, `SOPoolData`, `SOFeature` |
| Job struct | `*Job` | `MoveJob`, `MissileMoveJob`, `GridOverlapJob` |

**Job struct는 `t`를 안 붙인다.** 접미사 `Job`으로 구분 — Burst/Jobs 관례를 따르는 의도적 예외.

### SO 필드는 두 갈래

```csharp
// m_SO* — 이 클래스가 읽어서 자기 상태를 만드는 데이터 본체
[SerializeField] private SOAttackInfo  m_SOAttackInfo;
[SerializeField] private SOMonsterInfo m_SOMonsterInfo;

// m_ref* — 남을 찾아가는 열쇠 · 참조
[SerializeField] private SOPoolData m_refBulletPoolData;  // 풀 키
[SerializeField] private SONode     m_refRootNode;        // BT 루트
[SerializeField] private SOFeature  m_refRequire;         // 선행 카드
```

### ReactiveProperty는 백킹과 노출을 분리

```csharp
private readonly ReactiveProperty<int> m_rpExp = new();
public ReadOnlyReactiveProperty<int> Exp => m_rpExp;   // 외부에서 OnNext 못 하게
```

---

## 3. 파일 구조

파일당 하나의 타입, 파일 이름 = 주요 클래스명 (MonoBehaviour는 Unity 요구사항).
기본 `sealed` — 상속을 의도해 설계한 경우에만 푼다.

### using 아래 목적 주석 블록

제목 줄은 중앙 정렬, 본문은 `목적 :` 또는 `기능 :`으로 시작.
**무엇을 하는지가 아니라 왜 이 구조인지**를 적는다.

```csharp
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                    Laser
목적 : 오브젝트가 바라보는 방향으로 원통형 공격을 하는 오브젝트.
       PhysX 없이 ColliderManager.RaycastAllMask(레이 + 반경)로 판정한다 —
       몬스터가 PhysX Collider를 안 갖고 있어서 예전 TriggerStayObject
       (PhysX OnTriggerStay) 기반으로는 몬스터를 못 맞혔음.
 *///////////////////////////////////////////
```

### 멤버 순서

```csharp
public sealed class PlayerController : MonoBehaviour
{
    // 1. 직렬화된 필드
    // 2. private 필드 / 캐싱된 참조
    // 3. 프로퍼티
    // 4. Unity 생명주기: Awake, OnEnable, Start, FixedUpdate,
    //    Update, LateUpdate, OnDisable, OnDestroy
    // 5. public 메서드
    // 6. private 메서드
}
```

---

## 4. 제어 흐름

- 한 줄짜리 `if`는 **중괄호를 쓰지 않는다** — `if (x == null) return;`
- 핫 패스(`Update`, `FixedUpdate`)에서는 `foreach`보다 인덱스 `for`. 루프 변수는 `i`, 증가는 `++i`
- 매직 스트링 금지 — `nameof()`, `Animator.StringToHash()`, `Shader.PropertyToID()`
- 오른쪽 값에서 타입이 명백할 때만 `var`. 아니면 명시적 타입
- 모든 멤버에 명시적 접근 제한자 — 암묵적 `private` 금지
- 게임플레이 코드에서 **LINQ 금지**, 문자열 조합은 `StringBuilder`
- tag보다 layermask
- `!` 대신 `== false` / `== true`를 명시

```csharp
for (int i = 0; i < m_listWeapon.Count; ++i)
{
    if (m_listWeapon[i].gameObject.activeSelf == false)
        continue;

    if (m_listWeapon[i].CheckTime() == true)
        m_listWeapon[i].Fire(vTargetPos, m_refTargetScnner.Target);
}
```
