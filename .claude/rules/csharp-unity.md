# C# 스타일 — Unity 컨벤션

## 필드 선언

- 필드 네이밍: 멤버 변수는 m_ 접두사 사용 (예: m_vMoveSpeed)
- 매개변수는 '_' 사용 예시 ('Function(_fSpeed)))
- 변수 앞에 접두사 float(f), int (i), Vector2,3 (v), List (list), Queue (que), dobudle (d), string (str), Dictionary (hash), struct(t), enum(e)
- 매개변수는 '_' + 타입 접두사를 조합해서 사용 (예: `enum eFeatureTier`를 받는 매개변수는 `_eTier`, `struct Color`를 받는 매개변수는 `_tColor`)
- 클래스/메서드 네이밍: PascalCase 사용 (예: PlayerController)


```csharp
[SerializeField] private float m_fMoveSpeed = 5f;
[SerializeField] private Transform m_refSpawnPoint;

private Rigidbody m_refRigidbody;
private static readonly int JumpHash = Animator.StringToHash("Jump");
private const int MAX_JUMP_COUNT = 3;
```

## 캡슐화 (타협 불가)

**최소 가시성 원칙: 증명되지 않는 한 모든 것은 `private`입니다.**

- 필드: 기본적으로 `private`. 인스펙터에서 반드시 설정해야 하는 필드에만 `[SerializeField] private`을 사용하세요. `[SerializeField]`를 미리 추측해서 추가하지 마세요 — 디자이너/개발자가 실제로 인스펙터에서 그 값을 조정해야 할 때만 사용하세요.
- 메서드: 기본적으로 `private`. 다른 클래스가 실제로 호출할 때만 `public`으로 만드세요. "나중에 쓸모 있을지도 모른다"는 이유가 되지 않습니다.
- 프로퍼티: 기본적으로 `private`. 다른 클래스가 읽을 때만 public getter를 노출하세요. 다른 클래스가 쓸 때만 public setter를 노출하세요.
- 중첩 타입: 외부 접근이 필요하지 않으면 `private`.

**테스트 방법:** 무언가를 non-private으로 만들기 전에, 호출자를 특정하세요. 현재 코드베이스에서 구체적인 호출자를 명시할 수 없다면, `private`으로 남겨두세요. 에이전트는 추측성 public API 표면을 생성해서는 안 됩니다.

```csharp
// 나쁜 예 — "혹시 몰라서" 모든 것을 public으로 만듦
public class EnemySystem
{
    public EnemyModel Model;                    // private이어야 함
    public void Initialize() { }                // 내부에서만 호출됨
    public int CalculateDamage() { return 5; }  // 내부에서만 호출됨
    public void TakeDamage(int amount) { }      // 실제로 CombatSystem이 호출함 — 이건 괜찮음
}

// 좋은 예 — 최소한의 실질적인 가시성
public sealed class EnemySystem
{
    private readonly EnemyModel _model;
    
    private void Initialize() { }
    private int CalculateDamage() => 5;
    public void TakeDamage(int amount) { }  // CombatSystem이 이걸 호출함
}
```

**`[SerializeField]` 규율:**
```csharp
// 나쁜 예 — 인스펙터 노출이 필요 없는 필드를 직렬화함
[SerializeField] private int m_iCurrentHealth;           // 런타임 상태이지, 설정값이 아님 — 직렬화하지 마세요
[SerializeField] private bool m_bIsInitialized;          // 내부 플래그 — 직렬화하지 마세요
[SerializeField] private Transform m_refCachedTransform; // 캐싱된 참조 — 직렬화하지 마세요

// 좋은 예 — 디자이너가 설정하는 것만 직렬화함
[SerializeField] private float m_fMoveSpeed = 5f;         // 디자이너가 인스펙터에서 조정함
[SerializeField] private GameObject m_refBulletPrefab;      // 인스펙터 참조로 설정됨
private int m_iCurrentHealth;                             // 런타임 상태 — 그냥 private
private bool m_bIsInitialized;                            // 내부 플래그 — 그냥 private
```

## 타입과 네이밍

- 오른쪽 값에서 타입이 명백할 때는 `var`를 사용하세요. 명백하지 않으면 명시적 타입을 사용하세요
- 파일당 하나의 타입 — 파일 이름은 반드시 주요 클래스/구조체 이름과 일치해야 합니다 (MonoBehaviour에 대한 Unity의 요구사항)
- 기본적으로 `sealed` — 상속이 명시적으로 설계된 경우에만 봉인을 해제하세요
- 모든 것에 명시적 접근 제한자를 붙이세요 — 암묵적 `private`은 사용하지 마세요

## 구조 순서

```csharp
public sealed class PlayerController : MonoBehaviour
{
    // 1. 직렬화된 필드
    // 2. private 필드 / 캐싱된 참조
    // 3. 프로퍼티
    // 4. Unity 생명주기: Awake, OnEnable, Start, FixedUpdate, Update, LateUpdate, OnDisable, OnDestroy
    // 5. public 메서드
    // 6. private 메서드
}
```

## 제어 흐름

- 한 줄짜리 `if`는 중괄호를 사용하지 마세요
- 핫 패스(Update, FixedUpdate)에서는 `foreach`보다 `for`를 사용하세요
- 축약된 루프 변수를 쓰세요 — `for (int i = 0; ...)`
- 매직 스트링을 쓰지 마세요 — `nameof()`, `Animator.StringToHash()`, `Shader.PropertyToID()`를 사용하세요

## 기타

- 게임플레이 코드에서는 LINQ를 사용하지 마세요
- 문자열을 조합할 때는 `StringBuilder`를 사용하세요
- tag 보다는 layermask를 사용하세요
- using 밑에 부분에 해당 클래스의 목적을 서술하세요
```using System ...
/*///////////////////////////////////////////
                BulletLine
목적 : 원통형 메쉬를 시작점~끝점 사이에 걸치도록 Transform을 맞춰
       볼렛 예고선(텔레그래프)으로 사용하는 오브젝트
 *///////////////////////////////////////////
```
