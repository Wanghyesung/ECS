---
name: dotween
description: "DOTween 애니메이션 라이브러리 — 시퀀스 구성, 트윈 생명주기, 이징, 킬(kill) 전략. 중요: 누수와 에러를 막기 위해 항상 OnDestroy에서 트윈을 종료할 것."
globs: ["**/DOTween*", "**/*Tween*.cs", "**/*Animation*.cs"]
---

# DOTween 애니메이션 라이브러리

DOTween(Demigiant)은 Unity의 표준 트위닝 라이브러리다. transform, UI 요소, 머티리얼, 임의의 값을 최소한의 보일러플레이트로 애니메이션하는 플루언트(fluent), 체이닝 가능한 메서드를 제공한다.

## 기본 트윈

모든 축약 메서드는 `target.DO[Property](endValue, duration)` 패턴을 따른다.

```csharp
// Transform 트윈
transform.DOMove(new Vector3(0, 5, 0), 1f);           // 월드 포지션
transform.DOLocalMove(new Vector3(0, 5, 0), 1f);      // 로컬 포지션
transform.DOScale(Vector3.one * 1.5f, 0.3f);          // 스케일
transform.DORotate(new Vector3(0, 180, 0), 0.5f);     // 오일러 회전
transform.DOLocalRotateQuaternion(targetRot, 0.5f);   // 쿼터니언 회전

// UI 트윈 (CanvasGroup, Image 등)
m_refCanvasGroup.DOFade(0f, 0.5f);                    // 알파 페이드
m_refImage.DOColor(Color.red, 0.2f);                  // 색상 변경
m_refImage.DOFillAmount(1f, 1f);                       // 필 바(fill bar)
m_refRectTransform.DOAnchorPos(Vector2.zero, 0.3f);   // UI 위치

// 머티리얼 트윈 — renderer.material은 절대 사용하지 마라 (머티리얼을 복제해 배칭을 깨뜨린다).
// 인스턴스별 변경에는 MaterialPropertyBlock을 사용하고, 모든 인스턴스가 트윈을 공유한다면 공유 머티리얼을 트윈하라.
private static readonly int ColorId = Shader.PropertyToID("_Color");
private MaterialPropertyBlock m_propBlock;

Color colorFrom = Color.black;
DOTween.To(() => colorFrom, _color =>
{
    colorFrom = _color;
    m_propBlock.SetColor(ColorId, _color);
    m_refRenderer.SetPropertyBlock(m_propBlock);
}, Color.white, 0.1f);

// 임의의 값 트윈
float fValue = 0f;
DOTween.To(() => fValue, _fValue => fValue = _fValue, 10f, 1f);
```

## 시퀀스 구성 (Sequence Composition)

Sequence를 사용하면 여러 트윈을 체이닝하고, 겹치고, 하나의 단위로 오케스트레이션할 수 있다.

```csharp
Sequence seq = DOTween.Sequence();

// Append — 이전 트윈이 끝난 뒤에 재생된다
seq.Append(transform.DOMove(targetPos, 0.5f));
seq.Append(transform.DOScale(Vector3.one * 1.2f, 0.3f));

// Join — 이전 트윈과 동시에 재생된다
seq.Append(transform.DOMove(targetPos, 0.5f));
seq.Join(transform.DORotate(new Vector3(0, 360, 0), 0.5f));

// Insert — 시퀀스 내 특정 시간 위치에서 재생된다
seq.Insert(0.2f, m_refCanvasGroup.DOFade(1f, 0.3f));

// 인터벌과 콜백
seq.PrependInterval(0.5f);                             // 시퀀스 시작 전 딜레이
seq.AppendInterval(0.2f);                              // 트윈 사이의 정지
seq.AppendCallback(() => Debug.Log("Done!"));
seq.InsertCallback(1f, () => PlaySound());

// 시퀀스 설정
seq.SetLoops(3, LoopType.Yoyo);
seq.SetUpdate(true);                                   // 언스케일 타임 사용
seq.OnComplete(() => Destroy(gameObject));
```

### 중첩 시퀀스 (Nested Sequences)

```csharp
Sequence innerSeq = DOTween.Sequence();
innerSeq.Append(transform.DOScale(1.2f, 0.15f));
innerSeq.Append(transform.DOScale(1f, 0.15f));

Sequence outerSeq = DOTween.Sequence();
outerSeq.Append(transform.DOMove(targetPos, 0.5f));
outerSeq.Append(innerSeq);
```

## 이징 (Easing)

이징은 보간 곡선을 제어한다. 원하는 느낌에 맞춰 선택하라.

```csharp
transform.DOMove(target, 0.5f).SetEase(Ease.OutBounce);
transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutBack);          // 팝/오버슈트
transform.DOMove(target, 1f).SetEase(Ease.InOutQuad);          // 부드러운 시작/정지
m_refCanvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad);        // 가속하며 사라짐
```

### 게임 필(Game Feel)을 위한 대표적인 이징

| 이징 | 사용 사례 |
|------|----------|
| `Ease.OutBack` | 버튼 눌림 팝, 오버슈트와 함께 나타나는 요소 |
| `Ease.OutBounce` | 착지, 아이템 떨어짐 |
| `Ease.InOutQuad` | 부드러운 카메라 이동, 패널 슬라이드 |
| `Ease.OutQuad` | 자연스러운 감속, 가장 범용적으로 사용 |
| `Ease.InBack` | 예비 동작(anticipation)과 함께 사라지는 요소 |
| `Ease.OutElastic` | 통통 튀는, 장난스러운 UI 요소 |
| `Ease.Linear` | 프로그레스 바, 일정한 속도의 이동 |

### 커스텀 이징 커브

```csharp
[SerializeField] private AnimationCurve m_customEase;
transform.DOMove(target, 1f).SetEase(m_customEase);
```

## 중요: 트윈 생명주기와 킬 전략

**소유 오브젝트가 파괴될 때는 항상 트윈을 종료(kill)하라.** 파괴된 오브젝트를 대상으로 하는 트윈은 `MissingReferenceException`과 메모리 누수를 일으킨다.

```csharp
public sealed class AnimatedElement : MonoBehaviour
{
    private Tween m_activeTween;

    public void PlayAnimation()
    {
        // 새 트윈을 시작하기 전에 기존 트윈을 종료한다
        m_activeTween?.Kill();
        m_activeTween = transform.DOScale(1.2f, 0.3f)
            .SetEase(Ease.OutBack);
    }

    private void OnDestroy()
    {
        // 중요: 이 transform을 대상으로 하는 모든 트윈을 종료한다
        transform.DOKill();

        // SetId(this)를 사용했다면 ID로도 종료해야 한다:
        // DOTween.Kill(this);

        // 또는 저장해 둔 특정 트윈을 종료한다:
        // m_activeTween?.Kill();
    }
}
```

### 킬 메서드

```csharp
transform.DOKill();                  // 이 transform의 모든 트윈을 종료
transform.DOKill(true);              // 종료하면서 강제로 완료 처리
DOTween.Kill(this);                  // 이 오브젝트를 ID로 가진 트윈을 종료
DOTween.Kill("myTween");             // 문자열 ID를 가진 트윈을 종료
DOTween.KillAll();                   // 극단적 선택 — 모든 트윈을 종료
tween.Kill();                        // 특정 트윈 참조를 종료
```

## 트윈 ID

타겟팅된 조작을 위해 트윈에 ID를 태깅하라.

```csharp
transform.DOMove(target, 1f).SetId(this);          // 오브젝트 ID
transform.DOMove(target, 1f).SetId("uiTransition"); // 문자열 ID

// 이후: ID로 종료, 일시정지, 재생
DOTween.Kill("uiTransition");
DOTween.Pause(this);
DOTween.Play(this);
```

## SetAutoKill과 재사용 가능한 트윈

기본적으로 트윈은 완료 시 자동으로 파괴된다. 재사용 가능한 트윈을 만들려면 이를 비활성화하라.

```csharp
private Tween m_bounceTween;

private void Awake()
{
    m_bounceTween = transform.DOScale(1.2f, 0.15f)
        .SetEase(Ease.OutBack)
        .SetAutoKill(false)
        .SetLoops(2, LoopType.Yoyo)
        .Pause();                    // 일시정지 상태로 생성한 뒤 필요할 때 재생한다
}

public void Bounce()
{
    m_bounceTween.Restart();         // 처음부터 다시 재생
}

private void OnDestroy()
{
    m_bounceTween?.Kill();           // AutoKill이 꺼져 있으므로 수동으로 종료해야 한다
}
```

## SetUpdate — 언스케일 타임

게임이 일시정지되었을 때도(Time.timeScale = 0) 재생되어야 하는 애니메이션에 사용한다:

```csharp
// 게임이 일시정지된 상태에서도 일시정지 메뉴 페이드인은 재생된다
m_refCanvasGroup.DOFade(1f, 0.3f).SetUpdate(true);

// 시퀀스에도 동일하게 적용된다
DOTween.Sequence()
    .Append(panel.DOAnchorPos(Vector2.zero, 0.3f))
    .SetUpdate(true);
```

## SetCapacity — 시작 성능

애플리케이션 시작 시 한 번 호출하여 트윈 용량을 미리 할당하고 런타임 리사이징을 피하라.

```csharp
// 부트스트랩 MonoBehaviour나 RuntimeInitializeOnLoadMethod에서 호출한다
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void InitDOTween()
{
    DOTween.SetTweensCapacity(500, 50); // 트위너 500개, 시퀀스 50개
}
```

## 펀치와 셰이크 — 게임 주스(Game Juice)

피드백을 위한 짧고 임팩트 있는 애니메이션.

```csharp
// 펀치 — 원래 값으로 되돌아온다
transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 6, 0.5f);
transform.DOPunchPosition(new Vector3(0, 30, 0), 0.4f, 8, 0.5f);
transform.DOPunchRotation(new Vector3(0, 0, 15), 0.3f, 8, 0.5f);

// 셰이크 — 무작위 진동
transform.DOShakePosition(0.5f, strength: 10f, vibrato: 10, randomness: 90);
transform.DOShakeScale(0.3f, 0.5f);
transform.DOShakeRotation(0.5f, new Vector3(0, 0, 30));

// 카메라 셰이크
Camera.main.DOShakePosition(0.3f, 0.5f, 14, 90, false, true);
```

## 경로 트윈 (Path Tweens)

여러 웨이포인트를 따라 이동한다.

```csharp
Vector3[] waypoints = new[]
{
    new Vector3(0, 0, 0),
    new Vector3(5, 2, 0),
    new Vector3(10, 0, 0),
    new Vector3(15, 3, 0),
};

transform.DOPath(waypoints, 3f, PathType.CatmullRom)
    .SetEase(Ease.InOutQuad)
    .SetLookAt(0.01f);               // 이동 방향을 바라보게 한다
```

## 자주 쓰는 패턴

### 버튼 눌림 피드백

```csharp
public sealed class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private static readonly Vector3 PressScale = Vector3.one * 0.9f;

    public void OnPointerDown(PointerEventData _eventData)
    {
        transform.DOKill();
        transform.DOScale(PressScale, 0.1f).SetEase(Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        transform.DOKill();
        transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack);
    }

    private void OnDestroy() => transform.DOKill();
}
```

### 화면 전환

```csharp
public sealed class ScreenTransition : MonoBehaviour
{
    [SerializeField] private CanvasGroup m_refCanvasGroup;
    [SerializeField] private RectTransform m_refPanel;

    public Tween Show()
    {
        m_refCanvasGroup.alpha = 0f;
        m_refPanel.anchoredPosition = new Vector2(0, -50f);

        Sequence seq = DOTween.Sequence();
        seq.Append(m_refCanvasGroup.DOFade(1f, 0.25f));
        seq.Join(m_refPanel.DOAnchorPos(Vector2.zero, 0.3f).SetEase(Ease.OutQuad));
        return seq;
    }

    public Tween Hide()
    {
        Sequence seq = DOTween.Sequence();
        seq.Append(m_refCanvasGroup.DOFade(0f, 0.2f));
        seq.Join(m_refPanel.DOAnchorPos(new Vector2(0, 50f), 0.25f).SetEase(Ease.InQuad));
        return seq;
    }

    private void OnDestroy()
    {
        m_refCanvasGroup.DOKill();
        m_refPanel.DOKill();
    }
}
```

### 피격 플래시 (Damage Flash)

```csharp
public void FlashDamage(SpriteRenderer _refSpriteRenderer)
{
    _refSpriteRenderer.DOKill();
    Sequence seq = DOTween.Sequence();
    seq.Append(_refSpriteRenderer.DOColor(Color.red, 0.05f));
    seq.Append(_refSpriteRenderer.DOColor(Color.white, 0.15f));
    seq.SetId(_refSpriteRenderer);
}
```

### 수집 애니메이션 (Collect Animation)

```csharp
public void PlayCollectAnimation(Transform _refItem, Vector3 _vTargetUIPos)
{
    Sequence seq = DOTween.Sequence();
    seq.Append(_refItem.DOScale(1.3f, 0.15f).SetEase(Ease.OutBack));
    seq.Append(_refItem.DOMove(_vTargetUIPos, 0.4f).SetEase(Ease.InBack));
    seq.Join(_refItem.DOScale(0f, 0.3f).SetEase(Ease.InQuad));
    seq.OnComplete(() => Destroy(_refItem.gameObject));
}
```

## 안티패턴

### Update에서 트윈을 생성하지 마라

```csharp
// 나쁜 예 — 매 프레임 새 트윈을 생성해 심각한 누수를 일으킨다
private void Update()
{
    transform.DOMove(target.position, 0.5f);
}

// 좋은 예 — 한 번만 생성하고, 타겟만 다르게 갱신한다
private Tween m_moveTween;
public void MoveTo(Vector3 _vTarget)
{
    m_moveTween?.Kill();
    m_moveTween = transform.DOMove(_vTarget, 0.5f);
}
```

### 파괴 시 종료하는 것을 잊지 마라

```csharp
// 나쁜 예 — 오브젝트가 파괴된 후에도 트윈이 계속된다
public void Animate()
{
    transform.DOScale(2f, 5f).OnComplete(() => DoSomething());
}

// 좋은 예 — 항상 킬 전략을 마련해 둔다
private void OnDestroy() => transform.DOKill();
```

### 킬 전략 없는 무한 루프를 만들지 마라

```csharp
// 나쁜 예 — 멈출 방법이 없다
transform.DORotate(new Vector3(0, 360, 0), 2f, RotateMode.FastBeyond360)
    .SetLoops(-1, LoopType.Restart);

// 좋은 예 — 참조를 저장해 두고 OnDestroy에서 종료한다
private Tween m_spinTween;
private void Start()
{
    m_spinTween = transform.DORotate(new Vector3(0, 360, 0), 2f, RotateMode.FastBeyond360)
        .SetLoops(-1, LoopType.Restart)
        .SetId(this);
}
private void OnDestroy() => DOTween.Kill(this);
```

## 콜백

```csharp
transform.DOMove(target, 1f)
    .OnStart(() => Debug.Log("Started"))
    .OnUpdate(() => Debug.Log("Updating"))
    .OnComplete(() => Debug.Log("Done"))
    .OnKill(() => Debug.Log("Killed"))
    .OnStepComplete(() => Debug.Log("Loop step done"));
```

## 트윈 제어

```csharp
Tween tween = transform.DOMove(target, 1f);

tween.Pause();
tween.Play();
tween.Restart();
tween.Rewind();
tween.Complete();             // 끝으로 점프
tween.Goto(0.5f, true);      // 특정 시간으로 점프한 뒤 재생
tween.PlayForward();
tween.PlayBackwards();
tween.Flip();                 // 방향 반전
```
