---
name: unitask
description: "Unity를 위한 UniTask async/await — 무할당(zero-alloc) 비동기, 취소 토큰(CancellationToken), PlayerLoop 통합, 비동기 LINQ. '비동기 작업', '코루틴' 같은 단어가 언급되거나 코루틴을 UniTask로 대체해야 할 때 사용합니다."
globs: ["**/UniTask*", "**/*Async*.cs", "**/Cysharp*"]
---

# UniTask — Unity를 위한 무할당 Async/Await

UniTask(Cysharp)는 Unity의 PlayerLoop와 네이티브로 통합되고, GC 할당을 발생시키지 않으며, 제대로 된 취소(cancellation)를 지원하는 async/await를 제공한다. 모든 Unity 프로젝트에서 코루틴과 `System.Threading.Tasks.Task` 대신 UniTask를 우선적으로 사용하라.

## UniTask vs 코루틴 vs System.Threading.Tasks.Task

| 특성 | 코루틴 | Task | UniTask |
|---------|-----------|------|---------|
| GC 할당 | Enumerator + 박싱 | Task 오브젝트 + 상태 머신 | 없음(구조체 기반) |
| 취소 | 수동 플래그 | CancellationToken | CancellationToken |
| 반환값 | 불가 | 가능 | 가능 |
| 예외 처리 | 조용히 삼켜짐 | try/catch | try/catch |
| 스레드 풀에서 실행 | 불가 | 가능 (Unity에서는 위험함) | 불가 (PlayerLoop) |
| Awaitable 여부 | 불가 | 가능 | 가능 |

## 기본 사용법

### 메서드 시그니처

```csharp
using Cysharp.Threading.Tasks;

// Awaitable, 반환값 없음
public async UniTask LoadLevelAsync(CancellationToken _token)
{
    await UniTask.Delay(1000, cancellationToken: _token);
}

// Awaitable, 값을 반환함
public async UniTask<int> CalculateScoreAsync(CancellationToken _token)
{
    await UniTask.Yield(_token);
    return 100;
}

// Fire-and-forget (아껴서 사용하라, 호출 경계에서만)
public async UniTaskVoid OnButtonClickedAsync()
{
    await DoSomethingAsync(this.GetCancellationTokenOnDestroy());
}
```

### 중요: async void를 절대 사용하지 마라

```csharp
// 나쁜 예 — 예외가 조용히 삼켜지고, 취소도 안 되고, GC 할당도 발생함
public async void DoSomething() { ... }

// 좋은 예 — 올바른 에러 전파, 무할당
public async UniTask DoSomethingAsync(CancellationToken _token) { ... }

// 좋은 예 — 에러 로깅이 포함된 fire-and-forget
public async UniTaskVoid DoSomethingFireAndForget() { ... }
```

## 대기와 딜레이

```csharp
// 시간 기반 딜레이
await UniTask.Delay(1000, cancellationToken: _token);                    // 밀리초
await UniTask.Delay(TimeSpan.FromSeconds(1.5f), cancellationToken: _token);

// 프레임 기반 대기
await UniTask.Yield();                                                // 다음 프레임
await UniTask.Yield(PlayerLoopTiming.FixedUpdate);                   // 다음 FixedUpdate
await UniTask.NextFrame(_token);                                      // 명시적으로 다음 프레임
await UniTask.DelayFrame(5, cancellationToken: _token);               // N프레임 대기

// 조건 대기
await UniTask.WaitUntil(() => m_bIsReady, cancellationToken: _token);
await UniTask.WaitWhile(() => m_bIsLoading, cancellationToken: _token);
await UniTask.WaitUntilValueChanged(transform, _refTr => _refTr.position, cancellationToken: _token);

// Unity 비동기 오퍼레이션 래퍼
await SceneManager.LoadSceneAsync("GameScene").ToUniTask(cancellationToken: _token);
await Resources.LoadAsync<Texture2D>("myTexture").ToUniTask(cancellationToken: _token);
await UnityWebRequest.Get(url).SendWebRequest().ToUniTask(cancellationToken: _token);
```

## 취소 토큰 (Cancellation Tokens)

### 중요: 항상 취소 토큰을 전달하라

소유 오브젝트보다 오래 살아남는 비동기 작업은 `MissingReferenceException`과 정의되지 않은 동작을 일으킨다. 모든 비동기 메서드는 `CancellationToken`을 받아서 반드시 이를 존중해야 한다.

### 패턴 1: GetCancellationTokenOnDestroy (단순한 방식)

```csharp
public sealed class SimpleAsync : MonoBehaviour
{
    private async UniTaskVoid Start()
    {
        // 이 MonoBehaviour가 파괴되면 토큰이 자동으로 취소된다
        CancellationToken token = this.GetCancellationTokenOnDestroy();

        await UniTask.Delay(2000, cancellationToken: token);
        Debug.Log("This won't run if object was destroyed");
    }
}
```

### 패턴 2: 수동 CancellationTokenSource (Enable/Disable)

```csharp
public sealed class ManagedAsync : MonoBehaviour
{
    private CancellationTokenSource m_cts;

    private void OnEnable()
    {
        m_cts = new CancellationTokenSource();
        RunLoopAsync(m_cts.Token).Forget();
    }

    private void OnDisable()
    {
        m_cts?.Cancel();
        m_cts?.Dispose();
        m_cts = null;
    }

    private async UniTask RunLoopAsync(CancellationToken _token)
    {
        while (!_token.IsCancellationRequested)
        {
            await UniTask.Delay(1000, cancellationToken: _token);
            DoPeriodicWork();
        }
    }
}
```

### 패턴 3: 연결된 토큰 (Destroy + 수동 취소를 결합)

```csharp
public sealed class LinkedTokenExample : MonoBehaviour
{
    private CancellationTokenSource m_actionCts;

    public async UniTask PerformActionAsync()
    {
        // 이전 액션이 아직 실행 중이면 취소한다
        m_actionCts?.Cancel();
        m_actionCts?.Dispose();
        m_actionCts = new CancellationTokenSource();

        // 파괴 토큰과 연결해 둘 중 하나라도 발생하면 취소되게 한다
        CancellationToken destroyToken = this.GetCancellationTokenOnDestroy();
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            m_actionCts.Token, destroyToken);

        try
        {
            await DoWorkAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            // 취소 시 정상적으로 발생하는 예외 — 아무것도 하지 않는다
        }
        finally
        {
            linked.Dispose();
        }
    }
}
```

### OperationCanceledException 처리

```csharp
public async UniTask LoadDataAsync(CancellationToken _token)
{
    try
    {
        await SomeAsyncOperation(_token);
    }
    catch (OperationCanceledException)
    {
        // 정상적인 취소 — 조용히 정리한다
        return;
    }
    catch (Exception ex)
    {
        // 실제 에러 — 로그를 남기고 처리한다
        Debug.LogException(ex);
    }
}
```

## PlayerLoop 통합

UniTask는 정밀한 타이밍 제어를 위해 Unity의 PlayerLoop에 훅을 건다.

```csharp
// 사용 가능한 타이밍 포인트
await UniTask.Yield(PlayerLoopTiming.Initialization);
await UniTask.Yield(PlayerLoopTiming.EarlyUpdate);
await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
await UniTask.Yield(PlayerLoopTiming.PreUpdate);
await UniTask.Yield(PlayerLoopTiming.Update);
await UniTask.Yield(PlayerLoopTiming.PreLateUpdate);
await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

// FixedUpdate의 특정 타이밍을 대기
await UniTask.WaitForFixedUpdate(token);

// 프레임의 끝을 대기 (WaitForEndOfFrame 코루틴의 대체재)
await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
```

## WhenAll / WhenAny — 병렬 실행

```csharp
// 모든 태스크가 끝날 때까지 대기 (병렬)
(int iScore, string strName) = await UniTask.WhenAll(
    LoadScoreAsync(token),
    LoadNameAsync(token)
);

// 가장 먼저 끝나는 태스크를 대기
int iWinnerIndex = await UniTask.WhenAny(
    WaitForInputAsync(token),
    WaitForTimeoutAsync(5f, token)
);

// 결과를 포함한 타입 있는 WhenAny
(bool bHasResult, int iResult) = await UniTask.WhenAny(
    FetchFromCacheAsync(token),
    FetchFromNetworkAsync(token)
);

// 여러 에셋을 병렬로 로드
var textures = await UniTask.WhenAll(
    paths.Select(_strPath => LoadTextureAsync(_strPath, token))
);
```

## Forget과 Fire-and-Forget

```csharp
// Fire and forget — 예외는 Debug.LogException으로 로그가 남는다
DoSomethingAsync(token).Forget();

// 특정 취소 예외를 억제한다
DoSomethingAsync(token).SuppressCancellationThrow().Forget();
```

## UniTaskCompletionSource — 수동 완료

콜백 기반 API를 감싸거나 커스텀 awaitable 오퍼레이션을 만들 때 사용한다.

```csharp
public sealed class DialogSystem : MonoBehaviour
{
    private UniTaskCompletionSource<DialogResult> m_dialogTcs;

    public async UniTask<DialogResult> ShowDialogAsync(string _strMessage, CancellationToken _token)
    {
        m_dialogTcs = new UniTaskCompletionSource<DialogResult>();

        // 취소를 등록한다
        _token.Register(() => m_dialogTcs.TrySetCanceled());

        ShowDialogUI(_strMessage);
        return await m_dialogTcs.Task;
    }

    // UI 버튼에서 호출됨
    public void OnConfirmClicked() => m_dialogTcs.TrySetResult(DialogResult.Confirm);
    public void OnCancelClicked() => m_dialogTcs.TrySetResult(DialogResult.Cancel);
}
```

## 비동기 LINQ

UniTask는 이벤트 스트림을 위한 비동기 LINQ 연산자를 제공한다.

```csharp
using Cysharp.Threading.Tasks.Linq;

// 버튼 클릭으로부터의 비동기 이벤트 스트림
m_refButton.OnClickAsAsyncEnumerable()
    .ForEachAsync(_ =>
    {
        Debug.Log("Clicked");
    }, token);

// 입력 스로틀링
m_refButton.OnClickAsAsyncEnumerable()
    .ThrottleFirst(TimeSpan.FromSeconds(1))
    .ForEachAsync(_ => ProcessClick(), token);

// 채널 기반 producer/consumer
var channel = Channel.CreateSingleConsumerUnbounded<int>();
channel.Writer.TryWrite(42);
await channel.Reader.ReadAllAsync(token).ForEachAsync(_iItem => Process(_iItem));
```

## DOTween과의 통합

DOTween-UniTask 브릿지를 사용해 DOTween 애니메이션을 await하라.

```csharp
// 단일 트윈을 await
await transform.DOMove(targetPos, 1f)
    .SetEase(Ease.OutQuad)
    .ToUniTask(cancellationToken: token);

// 시퀀스를 await
Sequence seq = DOTween.Sequence();
seq.Append(transform.DOScale(1.2f, 0.2f));
seq.Append(transform.DOScale(1f, 0.2f));
await seq.ToUniTask(cancellationToken: token);

// 순차적인 애니메이션 체인
await transform.DOMove(pointA, 0.5f).ToUniTask(cancellationToken: token);
await transform.DOMove(pointB, 0.5f).ToUniTask(cancellationToken: token);
await transform.DOMove(pointC, 0.5f).ToUniTask(cancellationToken: token);
```

## Addressables와의 통합

```csharp
// 에셋 로드
GameObject prefab = await Addressables.LoadAssetAsync<GameObject>("EnemyPrefab")
    .ToUniTask(cancellationToken: token);

// 인스턴스화
GameObject instance = await Addressables.InstantiateAsync("EnemyPrefab", position, rotation)
    .ToUniTask(cancellationToken: token);

// 씬 로드
await Addressables.LoadSceneAsync("GameScene", LoadSceneMode.Additive)
    .ToUniTask(cancellationToken: token);
```

## 자주 쓰는 패턴

### 비동기 초기화 체인

```csharp
public sealed class GameBootstrap : MonoBehaviour
{
    private async UniTaskVoid Start()
    {
        CancellationToken token = this.GetCancellationTokenOnDestroy();

        try
        {
            await InitializeServicesAsync(token);
            await LoadPlayerDataAsync(token);
            await PreloadAssetsAsync(token);
            await LoadGameSceneAsync(token);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("Bootstrap cancelled");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
```

### 비동기 상태 머신

```csharp
public sealed class EnemyAI : MonoBehaviour
{
    private CancellationTokenSource m_cts;

    private void OnEnable()
    {
        m_cts = new CancellationTokenSource();
        RunAIAsync(m_cts.Token).Forget();
    }

    private void OnDisable()
    {
        m_cts?.Cancel();
        m_cts?.Dispose();
    }

    private async UniTask RunAIAsync(CancellationToken _token)
    {
        while (!_token.IsCancellationRequested)
        {
            await PatrolAsync(_token);
            await ChaseAsync(_token);
            await AttackAsync(_token);
            await UniTask.Yield(_token);
        }
    }

    private async UniTask PatrolAsync(CancellationToken _token)
    {
        while (!_token.IsCancellationRequested && !CanSeePlayer())
        {
            MoveToNextWaypoint();
            await UniTask.Delay(100, cancellationToken: _token);
        }
    }
}
```

### 타임아웃 래퍼

```csharp
public static async UniTask<T> WithTimeout<T>(
    UniTask<T> _task,
    float _fTimeoutSeconds,
    CancellationToken _token)
{
    int iWinnerIndex = await UniTask.WhenAny(
        _task,
        UniTask.Delay(TimeSpan.FromSeconds(_fTimeoutSeconds), cancellationToken: _token)
            .ContinueWith(() => default(T))
    );

    if (iWinnerIndex == 1)
        throw new TimeoutException($"Operation timed out after {_fTimeoutSeconds}s");

    return await _task;
}
```

### 디바운스된 입력

```csharp
private async UniTask ProcessSearchInputAsync(TMP_InputField _refInput, CancellationToken _token)
{
    string strPreviousText = string.Empty;

    while (!_token.IsCancellationRequested)
    {
        await UniTask.WaitUntilValueChanged(_refInput, _refI => _refI.text, cancellationToken: _token);

        // 디바운스: 마지막 변경 후 300ms 대기
        await UniTask.Delay(300, cancellationToken: _token);

        string strCurrentText = _refInput.text;
        if (strCurrentText != strPreviousText)
        {
            strPreviousText = strCurrentText;
            await PerformSearchAsync(strCurrentText, _token);
        }
    }
}
```

## 안티패턴

### 어디에서도 async void를 사용하지 마라

```csharp
// 나쁜 예 — 예외가 사라지고, 취소도 안 됨
public async void OnButtonClicked() { ... }

// 좋은 예
public async UniTaskVoid OnButtonClickedAsync()
{
    CancellationToken token = this.GetCancellationTokenOnDestroy();
    await HandleClickAsync(token);
}
```

### 취소 토큰을 빠뜨리지 마라

```csharp
// 나쁜 예 — 오브젝트가 파괴된 후에도 계속 실행된다
public async UniTask BadMethod()
{
    await UniTask.Delay(5000);
    transform.position = Vector3.zero; // 파괴되었다면 MissingReferenceException 발생
}

// 좋은 예
public async UniTask GoodMethod(CancellationToken _token)
{
    await UniTask.Delay(5000, cancellationToken: _token);
    transform.position = Vector3.zero;
}
```

### Unity에서 Task.Run이나 Task.Delay를 사용하지 마라

```csharp
// 나쁜 예 — 메인 스레드가 아닌 스레드 풀에서 실행됨
await Task.Run(() => transform.position = Vector3.zero);

// 나쁜 예 — System.Threading의 타이머이며 Unity 시간을 인식하지 못함
await Task.Delay(1000);

// 좋은 예
await UniTask.SwitchToMainThread();
await UniTask.Delay(1000);
```
