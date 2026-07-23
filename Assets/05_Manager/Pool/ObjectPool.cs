using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

/*///////////////////////////////////////////
               ObjectPool
기능 : 오브젝트를 미리 로드해두고 필요할 때 꺼내어 쓰면 반납할 수 있게 하는 클래스
       SOSceneData -> SOPoolData 목록을 외부(SceneController)에서 받아
       Addressables 비동기 로드 + UniTask 프레임 분산으로 프리워밍한다.
 *///////////////////////////////////////////

public class ObjectPool : MonoBehaviour
{
    public static ObjectPool m_Instance = null;
    private Dictionary<PoolObject, Queue<GameObject>> m_hashPool = new Dictionary<PoolObject, Queue<GameObject>>();
    private List<AssetReferenceGameObject> m_listLoadedRef = new List<AssetReferenceGameObject>();

    private const int c_iInstantiatePerFrame = 4;

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(this);

        m_Instance = this;
        DontDestroyOnLoad(this);
    }

    public async UniTask BuildPoolsAsync(List<SOPoolData> _listPoolData, CancellationToken _token = default)
    {
        ClearAllPools();

        if (_listPoolData == null)
            return;

        var listTasks = new List<UniTask>(_listPoolData.Count); // <- 풀링으로 변경 
        for (int i = 0; i < _listPoolData.Count; ++i)
            listTasks.Add(PrewarmOneAsync(_listPoolData[i], _token));

        await UniTask.WhenAll(listTasks);
    }

    private async UniTask PrewarmOneAsync(SOPoolData _refData, CancellationToken _token)
    {
        if (_refData == null || _refData.PrefabRef == null || _refData.PrefabRef.RuntimeKeyIsValid() == false)
        {
            Debug.Log("풀 프리팹 미설정 : ObjectPool");
            return;
        }

        var handle = _refData.PrefabRef.LoadAssetAsync();
        
        GameObject refPrefab = await _refData.PrefabRef.LoadAssetAsync()
            .ToUniTask(cancellationToken: _token, autoReleaseWhenCanceled: true);

        PoolObject refPrefabPoolObj = refPrefab.GetComponent<PoolObject>();
        if (refPrefabPoolObj == null)
        {
            Debug.Log("풀 프리팹에 PoolObject 없음 : ObjectPool");
            return;
        }

        m_listLoadedRef.Add(_refData.PrefabRef);

        Queue<GameObject> queGameObject = new Queue<GameObject>();
        m_hashPool[refPrefabPoolObj] = queGameObject;

        for (int i = 0; i < _refData.PreLoad; ++i)
        {
            GameObject refInstance = Instantiate(refPrefab);
            PoolObject instancePoolObj = refInstance.GetComponent<PoolObject>();
            instancePoolObj.SetOriginalPoolObj(refPrefabPoolObj);
            PushObject(refInstance);

            if (i % c_iInstantiatePerFrame == c_iInstantiatePerFrame - 1)
                await UniTask.Yield(PlayerLoopTiming.Update, _token);
        }
    }

    public void ClearAllPools()
    {
        foreach (var kvValue in m_hashPool)
        {
            Queue<GameObject> queValue = kvValue.Value;
            while (queValue.Count > 0)
            {
                GameObject refObj = queValue.Dequeue();
                if (refObj != null)
                    Destroy(refObj);
            }
        }
        m_hashPool.Clear();

        for (int i = 0; i < m_listLoadedRef.Count; ++i)
            m_listLoadedRef[i].ReleaseAsset();
        m_listLoadedRef.Clear();
    }

    public GameObject GetObject(PoolObject _refPrefabPoolObj)
    {
        if (_refPrefabPoolObj == null)
            return null;

        if (m_hashPool.TryGetValue(_refPrefabPoolObj, out var queValue) == false)
            return null;

        if (queValue.Count == 0)
            return null;

        GameObject refObject = queValue.Dequeue();
        IPoolable iPool = refObject.GetComponent<IPoolable>();
        if (iPool == null)
        {
            Debug.Log("오브젝트 풀에 이상한 오류 있음");
            return null;
        }

        refObject.transform.SetParent(null);
        iPool.Pop();
        refObject.gameObject.SetActive(true);

        return refObject;
    }

    public void PushObject(GameObject _refGameObj)
    {
        PoolObject refPoolObj = _refGameObj.GetComponent<PoolObject>();
        if (refPoolObj == null)
            return;

        if (m_hashPool.TryGetValue(refPoolObj.PoolKey, out var queValue) == false)
            return;

        if (refPoolObj.PushCount > 0)
            return;

        refPoolObj.Push();
        _refGameObj.transform.SetParent(transform);
        _refGameObj.gameObject.SetActive(false);

        queValue.Enqueue(_refGameObj);
    }

    public int GetObjectCount(PoolObject _refPrefabPoolObj)
    {
        if (m_hashPool.TryGetValue(_refPrefabPoolObj, out var queValue) == false)
            return -1;

        return queValue.Count;
    }
}
