---
name: addressables
description: "Addressables 에셋 로딩 — LoadAssetAsync, 핸들 생명주기, 라벨, 원격 카탈로그, 메모리 관리. '어드레서블(Addressable)을 통해 ~ 로드/관리한다'처럼 Addressable 기반 에셋 로딩·메모리 최적화를 언급할 때 사용합니다."
globs: ["**/Addressable*.cs", "**/*Address*"]
---

# Unity Addressables

## 설정

1. Package Manager를 통해 설치: `com.unity.addressables`
2. 인스펙터의 체크박스에서 에셋을 Addressable로 표시
3. Group으로 정리 (로컬, 원격, 씬별, 기능별)
4. 일괄 로딩을 위해 Label 지정 (예: "level1", "enemies", "ui")

### Group 구성 전략

```
Groups:
  Local_Static       -- 핵심 에셋, 항상 사용 가능 (셰이더, 필수 UI)
  Local_Dynamic      -- 빌드마다 변경되는 에셋
  Remote_Levels      -- 레벨별 에셋, 필요 시 다운로드
  Remote_Characters  -- 캐릭터 모델/애니메이션
```

## 에셋 로딩

### 기본 에셋 로딩

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class AddressableLoader : MonoBehaviour
{
    [SerializeField] private AssetReference m_refPrefab;

    private AsyncOperationHandle<GameObject> m_loadHandle;
    private GameObject m_refInstance;

    public async void LoadAndInstantiate()
    {
        m_loadHandle = Addressables.LoadAssetAsync<GameObject>(m_refPrefab);
        await m_loadHandle.Task;

        if (m_loadHandle.Status == AsyncOperationStatus.Succeeded)
        {
            m_refInstance = Instantiate(m_loadHandle.Result);
        }
        else
        {
            Debug.LogError($"Failed to load addressable: {m_loadHandle.OperationException}");
        }
    }

    // 중요: 메모리 누수를 막기 위해 핸들은 반드시 해제할 것
    private void OnDestroy()
    {
        if (m_loadHandle.IsValid())
        {
            Addressables.Release(m_loadHandle);
        }

        if (m_refInstance != null)
        {
            Destroy(m_refInstance);
        }
    }
}
```

### InstantiateAsync (로드 + 인스턴스화를 한 번에)

```csharp
public sealed class AddressableInstantiator : MonoBehaviour
{
    [SerializeField] private AssetReference m_refPrefab;
    private AsyncOperationHandle<GameObject> m_instanceHandle;

    public async void SpawnObject(Vector3 _vPosition, Quaternion _qRotation)
    {
        m_instanceHandle = Addressables.InstantiateAsync(m_refPrefab, _vPosition, _qRotation);
        await m_instanceHandle.Task;

        if (m_instanceHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError("Failed to instantiate addressable");
        }
    }

    private void OnDestroy()
    {
        // ReleaseInstance는 오브젝트를 파괴함과 동시에 핸들도 해제함
        if (m_instanceHandle.IsValid())
        {
            Addressables.ReleaseInstance(m_instanceHandle);
        }
    }
}
```

## 핸들 생명주기 (매우 중요)

`LoadAssetAsync`나 `InstantiateAsync`를 호출할 때마다 반환되는 핸들은 반드시 해제해야 합니다.

### 규칙

1. **모든 로드는 반드시 대응하는 해제가 있어야 합니다.** 예외 없음.
2. **모든 핸들을 추적하세요.** 필드나 리스트에 저장해두세요.
3. **파괴 시 해제하세요.** `OnDestroy`나 전용 정리 메서드를 사용하세요.
4. **해제 전에 `IsValid()`를 확인하세요.** 이중 해제 오류를 방지합니다.

### 핸들 추적 패턴

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class AddressableManager : MonoBehaviour
{
    private readonly List<AsyncOperationHandle> m_listHandles = new();

    public AsyncOperationHandle<T> LoadAsset<T>(object _key)
    {
        var refHandle = Addressables.LoadAssetAsync<T>(_key);
        m_listHandles.Add(refHandle);
        return refHandle;
    }

    public AsyncOperationHandle<GameObject> InstantiateAsset(AssetReference _refReference,
        Vector3 _vPosition = default, Quaternion _qRotation = default)
    {
        var refHandle = Addressables.InstantiateAsync(_refReference, _vPosition, _qRotation);
        m_listHandles.Add(refHandle);
        return refHandle;
    }

    public void ReleaseAll()
    {
        foreach (var refHandle in m_listHandles)
        {
            if (refHandle.IsValid())
            {
                Addressables.Release(refHandle);
            }
        }
        m_listHandles.Clear();
    }

    private void OnDestroy()
    {
        ReleaseAll();
    }
}
```

## 라벨 기반 로딩

동일한 라벨을 공유하는 여러 에셋을 한 번의 호출로 로드합니다.

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class LevelAssetLoader : MonoBehaviour
{
    private AsyncOperationHandle<IList<GameObject>> m_levelAssetsHandle;

    public async void LoadLevelAssets(string _strLevelLabel)
    {
        m_levelAssetsHandle = Addressables.LoadAssetsAsync<GameObject>(
            _strLevelLabel,
            _refPrefab =>
            {
                // 에셋이 로드될 때마다 각각 호출됨
                Debug.Log($"Loaded: {_refPrefab.name}");
            });

        await m_levelAssetsHandle.Task;

        if (m_levelAssetsHandle.Status == AsyncOperationStatus.Succeeded)
        {
            Debug.Log($"All {m_levelAssetsHandle.Result.Count} assets loaded for {_strLevelLabel}");
        }
    }

    public void UnloadLevelAssets()
    {
        if (m_levelAssetsHandle.IsValid())
        {
            Addressables.Release(m_levelAssetsHandle);
        }
    }
}
```

## Asset Reference 타입

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;

public sealed class TypedReferences : MonoBehaviour
{
    // 제네릭 참조 — UnityEngine.Object로 로드됨
    [SerializeField] private AssetReference m_refGeneric;

    // 타입이 지정된 참조 — 인스펙터에서 타입 안전성이 보장됨
    [SerializeField] private AssetReferenceGameObject m_refPrefab;
    [SerializeField] private AssetReferenceTexture2D m_refTexture;
    [SerializeField] private AssetReferenceSprite m_refSprite;
    [SerializeField] private AssetReferenceT<AudioClip> m_refAudio;
    [SerializeField] private AssetReferenceT<ScriptableObject> m_refData;

    // AssetLabelReference — 일괄 로딩을 위한 라벨 참조
    [SerializeField] private AssetLabelReference m_refEnemyLabel;

    public async void LoadTypedAssets()
    {
        var refPrefab = await m_refPrefab.LoadAssetAsync<GameObject>().Task;
        var refTexture = await m_refTexture.LoadAssetAsync<Texture2D>().Task;
        var refClip = await m_refAudio.LoadAssetAsync<AudioClip>().Task;
    }
}
```

## 메모리 누수 방지

### 흔한 실수

```csharp
// 나쁜 예: 핸들을 저장하지 않아 영원히 해제할 수 없음
public void LeakyLoad()
{
    Addressables.LoadAssetAsync<GameObject>("enemy"); // 핸들이 저장되지 않음!
}

// 나쁜 예: 너무 일찍 해제함
public async void TooEarlyRelease()
{
    var refHandle = Addressables.LoadAssetAsync<GameObject>("enemy");
    await refHandle.Task;
    var refInstance = Instantiate(refHandle.Result);
    Addressables.Release(refHandle); // 인스턴스가 아직 에셋을 사용 중인데 해제해버림!
}

// 좋은 예: 올바른 생명주기 관리
private AsyncOperationHandle<GameObject> m_handle;
private GameObject m_refInstance;

public async void ProperLoad()
{
    m_handle = Addressables.LoadAssetAsync<GameObject>("enemy");
    await m_handle.Task;
    m_refInstance = Instantiate(m_handle.Result);
}

private void OnDestroy()
{
    if (m_refInstance != null) Destroy(m_refInstance);
    if (m_handle.IsValid()) Addressables.Release(m_handle);
}
```

## 원격 카탈로그 업데이트

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;

public sealed class CatalogUpdater : MonoBehaviour
{
    public async void CheckForUpdates()
    {
        var refCheckHandle = Addressables.CheckForCatalogUpdates(false);
        await refCheckHandle.Task;

        if (refCheckHandle.Status == AsyncOperationStatus.Succeeded)
        {
            List<string> listCatalogs = refCheckHandle.Result as List<string>;
            if (listCatalogs != null && listCatalogs.Count > 0)
            {
                Debug.Log($"Found {listCatalogs.Count} catalog updates");
                var refUpdateHandle = Addressables.UpdateCatalogs(listCatalogs, false);
                await refUpdateHandle.Task;
                Debug.Log("Catalogs updated successfully");
                Addressables.Release(refUpdateHandle);
            }
        }
        Addressables.Release(refCheckHandle);
    }
}
```

## 에셋 프리로딩

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class AssetPreloader : MonoBehaviour
{
    [SerializeField] private List<AssetReference> m_listAssetsToPreload;
    private readonly List<AsyncOperationHandle> m_listPreloadHandles = new();

    public async Awaitable PreloadAll()
    {
        var listTasks = new List<System.Threading.Tasks.Task>();

        foreach (var refAssetRef in m_listAssetsToPreload)
        {
            var refHandle = Addressables.LoadAssetAsync<Object>(refAssetRef);
            m_listPreloadHandles.Add(refHandle);
            listTasks.Add(refHandle.Task);
        }

        await System.Threading.Tasks.Task.WhenAll(listTasks);
        Debug.Log($"Preloaded {m_listPreloadHandles.Count} assets");
    }

    public float GetProgress()
    {
        if (m_listPreloadHandles.Count == 0) return 0f;

        float fTotal = 0f;
        foreach (var refHandle in m_listPreloadHandles)
        {
            fTotal += refHandle.PercentComplete;
        }
        return fTotal / m_listPreloadHandles.Count;
    }

    private void OnDestroy()
    {
        foreach (var refHandle in m_listPreloadHandles)
        {
            if (refHandle.IsValid()) Addressables.Release(refHandle);
        }
        m_listPreloadHandles.Clear();
    }
}
```

## UniTask 연동

UniTask는 제로 할당(zero-allocation) async/await를 제공합니다. Addressable 핸들을 다음과 같이 변환하세요.

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

public sealed class AddressableUniTaskLoader : MonoBehaviour
{
    public async UniTask<GameObject> LoadPrefab(string _strAddress,
        System.Threading.CancellationToken _token = default)
    {
        var refHandle = Addressables.LoadAssetAsync<GameObject>(_strAddress);
        var refResult = await refHandle.ToUniTask(cancellationToken: _token);
        return refResult;
    }

    public async UniTask<T> LoadAsset<T>(AssetReferenceT<T> _refReference,
        System.Threading.CancellationToken _token = default) where T : Object
    {
        return await _refReference.LoadAssetAsync<T>().ToUniTask(cancellationToken: _token);
    }
}
```

## Addressables 프로파일러

Window > Asset Management > Addressables > Event Viewer를 사용해 다음을 확인하세요.
- 모든 활성 핸들과 참조 카운트 추적
- 해제되지 않은 핸들(메모리 누수) 식별
- 에셋 로드/언로드를 실시간으로 모니터링
- 어떤 Group이 로드되어 있는지 확인

## 빌드 스크립트

| 빌드 스크립트 | 사용 사례 |
|-------------|----------|
| Use Asset Database (fastest) | 에디터 개발용, 빌드 불필요 |
| Simulate Groups (Advanced) | 빌드 없이 Group 구조 테스트 |
| Use Existing Build | 프로덕션 런타임용, 사전 빌드된 번들 사용 |
| New Build | 새 AssetBundle 생성 |

플레이어를 빌드하기 전에 Addressables를 먼저 빌드하세요: Addressables Groups 창 > Build > New Build > Default Build Script.

### 스크립트를 통한 빌드

```csharp
#if UNITY_EDITOR
using UnityEditor.AddressableAssets.Settings;

public static class AddressableBuildHelper
{
    public static void BuildAddressables()
    {
        AddressableAssetSettings.BuildPlayerContent();
    }
}
#endif
```
