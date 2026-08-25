# 아키텍처 규칙

> ⚠️ **프로젝트 결정 사항:** 이 프로젝트는 **VContainer(DI)를 사용하지 않습니다.** 의존성은 직접 참조(SerializeField, GetComponent) 또는 절제된 싱글톤으로 연결합니다. 시스템 간 느슨한 통신(이벤트)에는 **C# 이벤트(`event Action<T>`)**를 표준으로 사용합니다 — 별도 메시징 라이브러리는 쓰지 않습니다.

## 프로젝트 전용: 게임 시스템 아키텍처 (필수)

> 아래는 이 프로젝트의 실제 게임플레이 시스템(몬스터 AI, 스킬, 이펙트) 설계 규칙입니다.

- **몬스터 Behavior Tree (BT):** Sequence, Selector, Action 노드로 구성된 트리 구조.
- **ScriptableObject(SO) 기반 Action:** 각 Action 로직은 SO로 작성되어 에디터에서 인스펙터로 할당 및 교체가 가능해야 함.
- **Blackboard System:** 몬스터 간의 상태, 타겟 정보, 동적 변수(int, float, bool 등)를 공유하고 전달하기 위한 Blackboard 필수 사용. `[Serializable]` 클래스로 작성해 몬스터(`Monster`/`BehaviorTree`)가 필드로 소유하고, BT 노드의 `Execute(BlackBoard _refBB)`에 참조로 전달한다. 별도 `MonoBehaviour` 컴포넌트일 필요는 없지만, "파일당 타입 하나" 규칙에 맞춰 `BlackBoard.cs`처럼 독립된 파일로 분리할 것을 권장.
- **데이터 분리 규칙 (중요):** SO는 '데이터와 에디터 세팅'만 가져야 하며, 런타임에 인스턴스별로 동적으로 변하는 상태값은 반드시 Blackboard나 런타임 노드 인스턴스에 저장할 것 — **SO 데이터 오염을 방지하기 위한 최우선 규칙**. 단, Composite 노드(Sequence/Select 등)처럼 BT 진입 시 `Instantiate`로 몬스터별로 클론되는 SO는 클론된 인스턴스에 한해 인덱스/타이머 같은 진행 상태를 직접 들고 있어도 된다 — 원본 에셋이 아니라 런타임 클론이기 때문. 클론되지 않는 leaf Action 노드는 절대 상태를 갖지 말 것(여러 몬스터가 같은 원본 SO 에셋을 공유하므로 즉시 오염됨).
- **Object Pool:** 자주 생성/삭제되는 미사일(Bullet), 이펙트(FX), 에너미(Enemy)에 필수 적용. (조커 카드/랜덤 스킬 시스템 특성상 발사체·이펙트 생성량이 많을 것으로 예상되므로 특히 중요 — 기획 배경은 `.claude/docs/game-design.md` 참고)
- **Event System:** 점수 갱신, 플레이어 피격, 게임 오버, 레벨업 UI, 조커 카드 UI 등 UI와 시스템 간 느슨한 결합(Observer 패턴)은 아래 **C# 이벤트(`event Action<T>`)**로 통일해서 구현할 것. 인스펙터에서 직접 바인딩해야 하는 UI 클릭 등은 `UnityEvent`를 써도 됨.

---

## 싱글톤 사용 가이드 (절제해서 사용)

DI를 쓰지 않기로 했으므로 싱글톤을 완전히 금지하진 않지만, 아무 데나 남발하면 예전의 "갓 GameManager" 문제로 되돌아갑니다. 아래 기준을 지키세요:

- **허용:** 씬 전체에 정말 하나만 존재해야 하고, 여러 시스템이 공통으로 참조하는 매니저급 클래스 (`AudioManager`, `SaveManager`, `ObjectPoolManager` 등)
- **금지:** `PlayerSystem`, `ScoreSystem`처럼 특정 기능 하나를 담당하는 클래스를 습관적으로 싱글톤화하는 것. 이런 건 씬 안에서 `[SerializeField]` 직접 참조로 연결하세요
- **금지:** 여러 시스템을 한데 묶어서 다 갖고 있는 `GameManager`/`GameContext` 같은 "만능 매니저" — 아래 "갓 오브젝트 금지" 참고
- `public static T Instance { get; private set; }` 형태의 프로퍼티를 사용하세요. `public static T m_Instance;`처럼 raw public 필드로 노출하지 마세요 — 외부에서 실수로 재할당할 수 있어 캡슐화 규칙(`csharp-unity.md`)에 위배됩니다.
- 싱글톤 클래스는 `Awake()`에서 중복 인스턴스를 파괴하는 표준 가드를 반드시 넣을 것 — **`return;`을 빠뜨리지 마세요.** `Destroy()`는 그 프레임 끝까지 실행이 지연되므로, `return`이 없으면 파괴 예정인 인스턴스가 그대로 `Instance`를 덮어써버립니다:

```csharp
public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return; // 필수 — 없으면 아래 두 줄이 파괴 예정 인스턴스에서도 실행된다
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
```

```csharp
// 실수 예시 — 이렇게 쓰면 안 됩니다
private void Awake()
{
    if (Instance != null && Instance != this)
    {
        Destroy(this); // return 없음 + 컴포넌트만 파괴(GameObject는 그대로 남음)
    }
    Instance = this; // 파괴 예정 인스턴스가 Instance를 다시 덮어씀 — 그 프레임 동안 죽은 싱글톤을 참조하게 된다
    DontDestroyOnLoad(gameObject);
}
```

## 시스템 간 통신을 위한 C# 이벤트

프로젝트 전반의 느슨한 결합(Observer 패턴)은 `event Action` / `event Action<T>`로 구현합니다. 별도 메시징 라이브러리(MessagePipe 등)는 쓰지 않습니다 — 씬 하나에 존재하는 시스템 수가 많지 않아 DI 컨테이너 없이도 구독/해제를 직접 관리할 수 있기 때문입니다.

```csharp
// --- 발행 측: 정적 이벤트로 선언 (구독자가 발행자 인스턴스를 몰라도 되게) ---
public sealed class Monster : MonoBehaviour
{
    public static event Action<Monster> OnMonsterDied;

    private void Die()
    {
        OnMonsterDied?.Invoke(this);
    }
}

// --- 구독 측: OnEnable에서 구독, OnDisable에서 해제 ---
public sealed class ScoreSystem : MonoBehaviour
{
    private void OnEnable()
    {
        Monster.OnMonsterDied += OnMonsterDied;
    }

    private void OnDisable()
    {
        Monster.OnMonsterDied -= OnMonsterDied;
    }

    private void OnMonsterDied(Monster _refMonster)
    {
        AddScore(_refMonster.ScoreValue);
    }

    private void AddScore(int _iAmount) { /* ... */ }
}
```

**규칙:**
- 구독은 반드시 짝을 맞춰 해제하세요 (`OnEnable`에서 `+=`, `OnDisable`에서 `-=`) — 짝이 안 맞으면 파괴된 오브젝트를 향한 호출이나 메모리 누수로 이어집니다
- 정적 이벤트는 씬 전환에도 살아남습니다 — 구독 해제를 빠뜨리면 이전 씬에서 파괴된 오브젝트가 계속 콜백을 받아 `MissingReferenceException`이 날 수 있으니 `OnDisable` 해제를 특히 꼼꼼히 챙기세요
- 인스펙터에서 직접 바인딩해야 하는 UI 클릭/드래그 등은 `UnityEvent`를 사용해도 됩니다 (`BaseButtonUI` 등 기존 UI 코드 패턴)
- 이벤트 인자로 매 프레임 새 객체를 만들지 마세요 — 이미 존재하는 참조(예: 죽은 `Monster` 자신)를 넘기거나 값 타입을 사용하세요
- 매니저 싱글톤(`ObjectPool.Instance`, `DungeonManager.Instance` 등)을 직접 호출하는 것도 이 프로젝트에서는 허용됩니다 — 이벤트는 "발행자가 구독자를 몰라야 하는 경우"에 쓰고, 그럴 필요가 없으면 직접 참조가 더 단순합니다

## 비동기를 위한 UniTask

UniTask는 코루틴을 완전히 대체합니다. `StartCoroutine`도, `IEnumerator`도, `yield return`도 사용하지 않습니다.

```csharp
public sealed class WaveSpawnerSystem : MonoBehaviour
{
    private readonly CancellationTokenSource m_cts = new();

    public async UniTaskVoid StartSpawning()
    {
        for (int iWaveIndex = 0; iWaveIndex < 10; iWaveIndex++)
        {
            await SpawnWave(iWaveIndex, m_cts.Token);
            await UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: m_cts.Token);
        }
    }

    private async UniTask SpawnWave(int _iWaveIndex, CancellationToken _token)
    {
        int iEnemyCount = _iWaveIndex * 3;
        for (int iEnemyIndex = 0; iEnemyIndex < iEnemyCount; iEnemyIndex++)
        {
            // 스폰 로직 (Object Pool에서 꺼내기)
            await UniTask.Delay(TimeSpan.FromSeconds(0.2f), cancellationToken: _token);
        }
    }

    private void OnDestroy() => m_cts.Cancel();
}
```

**규칙:**
- 항상 `CancellationToken`을 전달하세요 — 일반적으로 `this.GetCancellationTokenOnDestroy()`나 자체 `CancellationTokenSource`를 사용
- 대기 가능한 작업에는 `UniTask`를, fire-and-forget에는 `UniTaskVoid`를 사용하세요
- `new WaitForSeconds` 대신 `UniTask.Delay`를 사용하세요
- 병렬 비동기 작업에는 `UniTask.WhenAll`을 사용하세요
- 백그라운드 스레드에서 돌아올 때는 `UniTask.SwitchToMainThread()`를 사용하세요
- `async void`는 사용하지 마세요 — 항상 `async UniTask` 또는 `async UniTaskVoid`를 사용하세요

## 상속보다 조합을 우선

MonoBehaviour는 컴포넌트이지, 베이스 클래스가 아닙니다. 깊은 상속 트리를 만들지 마세요.

MonoBehaviour 상속 최대 깊이: 2 (베이스 + 서브클래스 1개). 그 이상이 필요하면 조합하세요.

View는 가벼워야 합니다 — 로직은 System에, 데이터는 Model에 있어야 합니다.

## UI는 MVP + 기능 분리 조합으로 구성

UI도 "상속보다 조합" 원칙을 그대로 따릅니다. 화면 하나를 담당하는 오케스트레이터 클래스가 선택 상태 추적, 상세 표시, 재화 비교, 트랜잭션, 카운트 조회까지 전부 직접 구현하지 않습니다. `DungeonManager`가 몬스터 스폰을 직접 하지 않고 전용 컴포넌트 `ObjectSpawner`를 `[SerializeField]`로 물고 있다가 위임하는 것처럼, UI 오케스트레이터도 각 표시/기능 단위를 별도 View 컴포넌트로 만들어 붙이고 위임합니다.

```csharp
// 나쁜 예 — 오케스트레이터 하나가 선택 상세 표시, 강화 트랜잭션, 재화 비교, 카운트 조회까지 전부 직접 처리
public class PlayerStatUI : MonoBehaviour, ICountable
{
    [SerializeField] private SOObjectInfo m_refBaseInfo;
    [SerializeField] private Container m_refContainer;

    [Header("선택 상세")]
    [SerializeField] private Image m_refSelectIcon;
    [SerializeField] private TextMeshProUGUI m_refNameText;
    [SerializeField] private TextMeshProUGUI m_refValueText;
    [SerializeField] private TextMeshProUGUI m_refLevelText;

    [Header("강화")]
    [SerializeField] private BaseButtonUI m_refUpgradeButton;
    [SerializeField] private TextMeshProUGUI m_refCostText;
    // RefreshSelected(), TryUpgrade(), GetCost(), GetLevel(), GetCount() 등
    // 모든 로직이 이 클래스 하나에 몰려있음 — 화면이 커질수록 God Object가 됨
}

// 좋은 예 — 오케스트레이터(Presenter)는 조합/위임만, 각 책임은 전용 View 컴포넌트로 분리
public sealed class PlayerStatUI : MonoBehaviour
{
    [SerializeField] private Container m_refContainer;
    [SerializeField] private StatDetailView m_refDetailView;     // 선택된 스탯 표시 전담
    [SerializeField] private UpgradeButtonView m_refUpgradeView; // 비용 표시 + 재화 비교 + 알파 처리 전담

    private void Awake()
    {
        m_refContainer.OnSelectEvt += m_refDetailView.Show;
        m_refContainer.OnSelectEvt += m_refUpgradeView.Bind;
    }
}
```

**규칙:**
- MVP의 View는 "표시만" 담당하는 순수 컴포넌트(`StatDetailView`, `UpgradeButtonView` 등)로 쪼갭니다 — MVS의 View와 동일하게 로직 없이 `Show()`/`Refresh()`류 메서드만 가집니다
- Presenter 역할은 오케스트레이터(`PlayerStatUI` 같은 클래스)가 맡되, `DungeonManager`처럼 `[SerializeField]`로 전용 View 컴포넌트를 직접 소유하고 이벤트 구독이나 메서드 호출로 위임합니다 — 오케스트레이터는 "무엇을 언제 보여줄지"만 결정하고 "어떻게 그리는지"는 각 View 컴포넌트가 담당합니다
- `ICountable`처럼 인터페이스를 구현해야 하는 책임도 오케스트레이터가 직접 들기보다, 필요하면 전용 소스 컴포넌트로 분리하는 걸 먼저 검토합니다
- 판단 기준: 그 필드/메서드 묶음이 "다른 화면에서도 재사용될 만한 독립된 표시/기능 단위"인가요? 그렇다면 별도 컴포넌트로 뺍니다. 이 화면에서만 쓰이는 단순 배선(이벤트 구독/해제 정도)이라면 오케스트레이터에 남겨도 됩니다

## 정적 데이터를 위한 ScriptableObject

아이템, 어빌리티, 적 설정, 레벨 데이터, BT Action — 이 모두는 ScriptableObject여야 합니다:

```csharp
[CreateAssetMenu(menuName = "Game/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    [SerializeField] private string m_strDisplayName;
    [SerializeField] private float m_fDamage;
    [SerializeField] private float m_fFireRate;
    [SerializeField] private GameObject m_refBulletPrefab;
}
```

ScriptableObject는 **정적/설정 데이터**를 담습니다. 런타임에 변경 가능한 상태는 절대 SO에 넣지 말고 Model이나 Blackboard에 두세요 (위 "데이터 분리 규칙" 참고).



## 갓 오브젝트(God Object) 금지

```csharp
// 나쁜 예
class GameManager : MonoBehaviour
{
    // 점수, 목숨, 스폰, UI, 오디오, 저장, 입력, 일시정지... 모든 걸 처리함
}

// 좋은 예 — 책임별로 나뉜 별도의 System
// PlayerSystem — 체력, 이동
// ScoreSystem — 점수, 콤보
// SpawnSystem — 적 웨이브
// 각각은 자기 책임만 가지며, 씬에서 SerializeField로 필요한 것만 연결
```

## 씬 구성

- 부트스트랩 씬(또는 첫 씬)에 매니저급 싱글톤(`AudioManager`, `SaveManager`, `ObjectPoolManager` 등)을 배치하고 `DontDestroyOnLoad`로 유지
- 게임 씬은 그 위에서 필요한 System/View만 배치
- 씬 로드/언로드는 UniTask를 통해 비동기로 처리합니다:

```csharp
await SceneManager.LoadSceneAsync("GameScene", LoadSceneMode.Additive).ToUniTask();
```
