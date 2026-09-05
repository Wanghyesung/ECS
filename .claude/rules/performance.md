# 성능 규칙

## 황금 규칙

**Update, FixedUpdate, LateUpdate에서 힙 할당은 절대 금지입니다.**

모든 할당은 GC를 유발하고, 이는 프레임 스파이크를 일으킵니다. Unity 프로파일러의 GC Alloc 열로 프로파일링하세요.

## 모든 것을 캐싱하세요

```csharp
// 나쁜 예 — 매 프레임마다 FindObjectOfType
private void Update()
{
    Camera.main.WorldToScreenPoint(transform.position); // Camera.main은 내부적으로 FindObjectOfType를 호출함
    GetComponent<Rigidbody>().AddForce(Vector3.up);
}

// 좋은 예 — Awake에서 캐싱
private Camera _mainCamera;
private Rigidbody _rigidbody;

private void Awake()
{
    _mainCamera = Camera.main;
    _rigidbody = GetComponent<Rigidbody>();
}
```

다음은 Awake에서 캐싱하세요 — Update에서는 절대 호출하지 마세요:
- `GetComponent<T>()` / `TryGetComponent<T>()`
- `Camera.main` (내부적으로 FindObjectOfType를 수행함)
- `transform` / `gameObject` (사소해 보이지만 핫 루프에서 누적됨)
- `Animator.StringToHash()` / `Shader.PropertyToID()` → `static readonly int` (UpperCamelCase)

## 할당을 피하세요

| 할당 발생 | 대신 사용할 것 |
|-----------|------------|
| Update에서 `new List<T>()` | 미리 할당하고 `.Clear()`로 재사용 |
| `new WaitForSeconds(n)` | 필드로 캐싱: `WaitForSeconds _wait = new(0.5f)` |
| `string + string` | `StringBuilder` 또는 `string.Format` |
| List가 아닌 것에 `foreach` | 인덱스를 사용한 `for` 루프 |
| `FindObjectOfType` | 캐싱된 참조 또는 SO 런타임 세트 |
| `SendMessage` / `BroadcastMessage` | 직접 참조 또는 이벤트 |
| `Physics.RaycastAll` | 미리 할당된 배열과 함께 `Physics.RaycastNonAlloc` |

## 물리(Physics)

- 비할당(non-allocating) 변형을 사용하세요: `OverlapSphereNonAlloc`, `RaycastNonAlloc`, `SphereCastNonAlloc`
- 결과 배열을 미리 할당하세요: `private RaycastHit[] _hitBuffer = new RaycastHit[16]`
- 물리 쿼리는 `Update`가 아닌 `FixedUpdate`에서 수행하세요

## 오브젝트 생명주기

- 자주 인스턴스화되는 오브젝트는 풀링하세요 — `ObjectPool<T>` 또는 커스텀 풀
- 풀로 반환할 때는 `Destroy`가 아닌 `SetActive(false)`를 사용하세요
- `DontDestroyOnLoad`는 아껴서 사용하세요 — 부트스트래퍼 씬을 우선하세요

## 렌더링과 드로우 콜 (타협 불가)

**드로우 콜 예산은 GC만큼이나 중요합니다.** 아키텍트는 반드시 처음부터 렌더링 최적화를 계획해야 합니다 — 나중에 생각하는 게 아닙니다.

### 드로우 콜 규칙

고유한 머티리얼 + 메시 조합 하나 = 드로우 콜 1개. 항상 **가능한 가장 낮은 드로우 콜 수**를 목표로 하세요 — 공격적으로 배칭하고, 모든 것을 아틀라스로 만들고, 머티리얼을 공유하세요. "이 정도면 충분하다"는 숫자는 없습니다 — 항상 더 적을수록 좋습니다.

### 스프라이트 및 텍스처 아틀라싱 (2D에서는 필수)

```
// 나쁜 예 — 카드 스프라이트 52개 = 개별 텍스처 52개 = 드로우 콜 52개 이상
card_hearts_1.png, card_hearts_2.png, ... (개별 파일들)

// 좋은 예 — 스프라이트 아틀라스 1개 = 모든 카드에 대해 드로우 콜 1개
SpriteAtlas "CardAtlas"에 카드 52장 + 뒷면 + UI 요소를 모두 포함
```

**규칙:**
- **모든 2D 스프라이트는 반드시 스프라이트 아틀라스를 사용해야 합니다** — `Assets/Art/Atlases/`에 아틀라스를 생성하세요
- 렌더링 레이어별로 그룹화하세요: 논리적 그룹당 아틀라스 하나(카드, UI 아이콘, 환경 타일)
- 최대 아틀라스 크기: 2048x2048(모바일) 또는 4096x4096(데스크톱)
- 최적의 패킹을 위해 "Tight Packing"과 "Allow Rotation"을 활성화하세요
- 아키텍트는 반드시 TDD에 아틀라스 그룹화 방식을 명시해야 합니다

### 머티리얼 공유

```csharp
// 나쁜 예 — 오브젝트마다 머티리얼 인스턴스를 생성함 (배칭이 깨짐)
renderer.material.color = Color.red;  // .material은 머티리얼을 복제함!

// 좋은 예 — 공유 머티리얼 + MaterialPropertyBlock (배칭이 유지됨)
private static readonly int ColorId = Shader.PropertyToID("_Color");
private MaterialPropertyBlock _propBlock;

private void Awake()
{
    _propBlock = new MaterialPropertyBlock();
}

public void SetColor(Color color)
{
    _propBlock.SetColor(ColorId, color);
    _renderer.SetPropertyBlock(_propBlock);
}
```

**규칙:**
- `renderer.material`에 절대 접근하지 마세요 — 머티리얼을 복제하고 배칭을 깨뜨립니다
- 읽기 전용 접근에는 `renderer.sharedMaterial`을 사용하세요
- 인스턴스별 속성 변경에는 `MaterialPropertyBlock`을 사용하세요
- 가능한 곳마다 오브젝트 간 머티리얼을 공유하세요 — 고유 머티리얼이 적을수록 드로우 콜도 적어집니다

### SRP 배처와 배칭

```csharp
// URP/HDRP의 경우 — SRP 배처 호환성:
// 셰이더는 CBUFFER 블록을 사용해야 합니다 (Shader Graph는 이를 자동으로 처리함)
// 동일한 셰이더 변형을 사용하는 모든 머티리얼이 함께 배칭됩니다
```

**규칙:**
- **URP 프로젝트**: SRP 배처가 활성화되어 있는지 확인하세요 (Project Settings → Graphics)
- **스프라이트**: 스프라이트 아틀라스 + 동일한 머티리얼 = 자동 배칭
- **3D**: 반복되는 메시(나무, 소품, 적)에 대해 머티리얼에서 GPU 인스턴싱을 활성화하세요
- **정적 오브젝트**: 인스펙터에서 "Batching Static"으로 표시하여 정적 배칭을 적용하세요
- **동적 오브젝트**: 동적 배칭을 위해 동일한 머티리얼 + 메시를 유지하세요 (정점 300개 미만)
- 아키텍트는 반드시 TDD에 배칭 전략을 명시해야 합니다

### UI 캔버스 최적화

```
// 나쁜 예 — 모든 것에 캔버스 하나
Canvas (root)
  ├─ HUD (매 프레임 갱신됨)
  ├─ PauseMenu (거의 변경되지 않음)
  └─ ScorePopups (자주 스폰됨)

// 좋은 예 — 갱신 빈도별로 분리
Canvas_HUD (매 프레임 갱신됨)
Canvas_Static (일시정지 메뉴, 설정 — 거의 재구축되지 않음)
Canvas_Popups (동적 요소)
```

**규칙:**
- **캔버스를 갱신 빈도별로 분리하세요** — 변경되는 요소 하나가 캔버스 메시 전체를 재구축합니다
- 정적 UI(배경, 절대 변하지 않는 라벨)는 별도의 캔버스에 두세요
- 자주 갱신되는 UI(체력바, 타이머, 점수)는 자신만의 캔버스에 두세요
- 클릭/터치 감지가 필요 없는 요소에서는 `Raycast Target`을 비활성화하세요
- 재활성화 시 재구축을 피하기 위해 `SetActive(false)` 대신 `CanvasGroup.alpha = 0` + `blocksRaycasts = false`를 사용하세요
- UI 요소(팝업, 리스트 아이템)를 풀링하세요 — Instantiate/Destroy를 하지 마세요

### 오버드로우

- 겹치는 투명 스프라이트를 최소화하세요 — 각 레이어는 별도의 드로우입니다
- 가능하면 불투명 스프라이트를 사용하세요 (알파 없음)
- 파티클 효과의 경우: 작은 파티클을 많이 쓰는 것보다 큰 파티클을 적게 쓰는 것이 낫습니다
- Scene View → Overdraw 시각화 모드로 오버드로우를 확인하세요

### 카메라와 컬링

- 적절한 near/far 클립 평면을 설정하세요 — 카메라가 볼 수 없는 것을 렌더링하지 마세요
- 필요 없는 오브젝트를 카메라에서 제외하기 위해 컬링 레이어를 사용하세요
- 2D의 경우: Z-position 트릭이 아니라 `Sorting Layers`와 `Order in Layer`를 사용하세요

### 아키텍트의 책임

TDD는 반드시 다음을 다루는 **렌더링 전략** 섹션을 포함해야 합니다:
1. 드로우 콜을 어떻게 최소화할 것인지 (가능한 가장 낮은 수를 목표로)
2. 아틀라스 계획 (어떤 스프라이트가 어떤 아틀라스에 들어가는지)
3. 머티리얼 공유 전략
4. 배칭 방식 (SRP 배처, 정적, 동적, GPU 인스턴싱)
5. UI 캔버스 분리 계획
6. 알려진 오버드로우 위험 및 완화 방안

이것은 선택 사항이 아닙니다. 드로우 콜 500개 때문에 10 FPS로 돌아가는 게임은 최적화되지 않은 C#이더라도 60 FPS를 유지하는 게임보다 나쁩니다.

### 개발자 액션 아이템 (필수)

에이전트는 항상 Unity 에셋(스프라이트 아틀라스, 머티리얼 프리셋, 텍스처 임포트 설정, 라이팅 베이크 등)을 직접 생성할 수는 없습니다. 렌더링 최적화에 수동 Unity 에디터 작업이 필요할 때:

1. **조용히 건너뛰지 마세요.** 게임에 스프라이트 아틀라스가 필요한데 에이전트가 만들 수 없다면, 에이전트는 반드시 멈추고 개발자에게 알려야 합니다.
2. 개발자가 Unity 에디터에서 따라할 수 있도록 **명확한 단계별 지침**을 생성하세요. 구체적으로: 어느 메뉴, 어떤 설정, 어떤 에셋을 포함할지.
3. **의존 작업의 진행을 막으세요.** 아직 존재하지 않는 아틀라스나 공유 머티리얼을 참조하는 코드를 작성하지 마세요. 개발자가 먼저 에셋을 만들도록 안내한 후 계속하세요.
4. **아키텍트는 TDD에 "개발자 설정 단계" 섹션을 포함**해서 개발자가 구현 전이나 도중에 해야 할 모든 수동 최적화 작업을 나열해야 합니다.
5. **리뷰어는 이 단계들이 완료되었는지 확인합니다.** 스프라이트 아틀라스가 계획되었지만 존재하지 않는다면, 리뷰는 실패하며 개발자를 위한 지침과 함께 반려됩니다.

지침 형식 예시:
```
## 개발자 액션 필요: 스프라이트 아틀라스 설정

최적의 드로우 콜 수를 위해 게임에는 스프라이트 아틀라스가 필요합니다. Unity 에디터에서 다음을 생성해 주세요:

1. Project 창에서 우클릭 → Create → 2D → Sprite Atlas
2. "CardAtlas"로 이름을 짓고, Assets/Art/Atlases/에 저장
3. 인스펙터에서:
   - "Objects for Packing"에 "Assets/Art/Cards" 폴더를 추가
   - "Max Texture Size"를 2048로 설정
   - "Tight Packing" 활성화
   - "Allow Rotation" 활성화
   - "Pack Preview"를 클릭해 모든 스프라이트가 맞는지 확인
4. Assets/Art/UI/로 "UIAtlas"에 대해 반복

이 아틀라스들이 존재할 때까지, 모든 카드/UI 요소는 별도의 드로우 콜입니다.
```

이는 에이전트가 만들 수 없는 모든 최적화 에셋에 적용됩니다: 스프라이트 아틀라스, 머티리얼 프리셋, 텍스처 압축 설정, 라이트맵 베이킹, 오클루전 컬링 설정, LOD 그룹 등.

## 디버그

- 프로덕션에서는 `Debug.Log`를 사용하지 마세요 — `[Conditional("UNITY_EDITOR")]` 래퍼를 사용하세요
- 런타임 체크가 아닌 스크립팅 정의로 디버그 코드를 제거하세요
