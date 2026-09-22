# 성능 규칙

## 황금 규칙

**Update, FixedUpdate, LateUpdate에서 힙 할당은 절대 금지입니다.** 모든 할당은 GC 스파이크입니다. 프로파일러의 GC Alloc 열로 확인하세요.

## 캐싱

```csharp
// 나쁜 예 — 매 프레임 탐색
private void Update()
{
    Camera.main.WorldToScreenPoint(transform.position); // Camera.main = FindObjectOfType
    GetComponent<Rigidbody>().AddForce(Vector3.up);
}

// 좋은 예 — Awake에서 캐싱
private Camera m_refMainCamera;
private Rigidbody m_refRigidbody;

private void Awake()
{
    m_refMainCamera = Camera.main;
    m_refRigidbody = GetComponent<Rigidbody>();
}
```

Awake에서 캐싱하고 Update에서 절대 호출하지 말 것: `GetComponent<T>()`/`TryGetComponent`, `Camera.main`, `FindObjectOfType`, `Animator.StringToHash()`/`Shader.PropertyToID()` → `static readonly int`.

## 할당 회피표

| 할당 발생 | 대신 |
|---|---|
| Update에서 `new List<T>()` | 필드로 미리 할당 + `.Clear()` |
| `string + string` | `StringBuilder` / `string.Format` |
| List가 아닌 컬렉션에 `foreach` | 인덱스 `for` |
| `FindObjectOfType` | 캐싱된 참조 / `[SerializeField]` |
| `SendMessage` / `BroadcastMessage` | 직접 참조 또는 `event Action<T>` |
| `Physics.RaycastAll` 등 | `RaycastNonAlloc` / `OverlapSphereNonAlloc` + 미리 할당한 배열 (`private RaycastHit[] m_arrHitBuffer = new RaycastHit[16]`) |
| 코루틴 `new WaitForSeconds` | UniTask (`UniTask.Delay`) |
| LINQ | 게임플레이 코드에서 금지 |

물리 쿼리는 `Update`가 아닌 `FixedUpdate`에서.

## 오브젝트 생명주기

- 자주 생성/삭제되는 Bullet/FX/Enemy는 **반드시 이 프로젝트의 풀**([[object-pooling]] 스킬: `SOPoolData` + `PoolObject` + `ObjectPoolManager`)을 사용 — `Instantiate`/`Destroy` 직접 호출 금지
- 풀 반환은 `SetActive(false)`; `DontDestroyOnLoad`는 매니저급에만

## 렌더링 / 드로우 콜

고유한 (머티리얼 + 메시) 조합 하나 = 드로우 콜 1개. 항상 더 적게.

- **`renderer.material`에 절대 접근하지 마세요** — 머티리얼을 복제해 배칭이 깨집니다. 읽기는 `sharedMaterial`, 인스턴스별 변경은 `MaterialPropertyBlock`:

```csharp
private static readonly int ColorId = Shader.PropertyToID("_Color");
private MaterialPropertyBlock m_refPropBlock;
private Renderer m_refRenderer;

private void Awake()
{
    m_refRenderer = GetComponent<Renderer>();
    m_refPropBlock = new MaterialPropertyBlock();
}

public void SetColor(Color _tColor)
{
    m_refPropBlock.SetColor(ColorId, _tColor);
    m_refRenderer.SetPropertyBlock(m_refPropBlock);
}
```

- URP: SRP Batcher 활성 확인 (Project Settings → Graphics). 같은 셰이더 변형끼리 자동 배칭
- 반복 메시(적, 총알, 소품): 머티리얼에서 **GPU Instancing** 활성
- 정적 오브젝트: `Batching Static`; 동적 오브젝트: 같은 머티리얼+메시 유지(정점 300개 미만이면 동적 배칭)
- 파티클: 작은 파티클 많이보다 큰 파티클 적게 (오버드로우). Scene View → Overdraw 모드로 확인
- UI 스프라이트/아이콘은 **Sprite Atlas**(`Assets/Art/Atlases/`)로 묶어 드로우 콜 1개로. 에이전트가 아틀라스 에셋을 만들 수 없으면 **조용히 넘어가지 말고** 사용자에게 생성 단계(메뉴·설정·포함 폴더)를 안내한 뒤 진행

## UI 캔버스

- **캔버스를 갱신 빈도별로 분리** — 요소 하나가 바뀌면 그 캔버스 전체가 재구축됨: `Canvas_HUD`(매 프레임) / `Canvas_Static`(메뉴) / `Canvas_Popups`(동적)
- 클릭이 필요 없는 요소는 `Raycast Target` 해제
- 숨김은 `SetActive(false)` 대신 `CanvasGroup.alpha = 0` + `blocksRaycasts = false`
- 팝업/리스트 아이템도 풀링

## 디버그

- 프로덕션 `Debug.Log` 금지 — `[Conditional("UNITY_EDITOR")]` 래퍼 사용
- 디버그 코드는 런타임 체크가 아니라 스크립팅 정의로 제거
