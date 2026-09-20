using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/*///////////////////////////////////////////
               ObjectPool
기능 : 상시 풀과 씬 전용 풀의 수명을 분리하고, 미리 로드한 오브젝트를 재사용한다.
 *///////////////////////////////////////////
public sealed class ObjectPoolManager : MonoBehaviour
{
    private sealed class PoolEntry
    {
        public bool IsStatic;

        public AsyncOperationHandle<GameObject> Handle;
        public PoolObject Prefab;
        public int ActiveCap; //동시에 활성화할 수 있는 수
        public readonly Stack<GameObject> Pool = new Stack<GameObject>();
        public readonly List<PoolObject> Instances = new List<PoolObject>();
        public readonly LinkedList<GameObject> ActiveInstances = new LinkedList<GameObject>();
    }

    private sealed class PoolLoadProgress
    {
        private readonly int m_iTotalCount;
        private readonly IProgress<float> m_refProgress;
        private int m_iCompletedCount;

        public PoolLoadProgress(int _iTotalCount, IProgress<float> _refProgress)
        {
            m_iTotalCount = _iTotalCount;
            m_refProgress = _refProgress;
        }

        public void CompleteOne()
        {
            ++m_iCompletedCount;
            m_refProgress?.Report((float)m_iCompletedCount / m_iTotalCount);
        }
    }

    private struct tTimeData
    {
        public float fPushTime;
        public PoolObject refPoolObj;
        public int iGeneration;

        public tTimeData(float _fExpireTime, PoolObject _refPoolObj, int _iGeneration)
        {
            fPushTime = _fExpireTime;
            refPoolObj = _refPoolObj;
            iGeneration = _iGeneration;
        }
    }

    private struct tExpireTimeComparer : IComparer<tTimeData>
    {
        public int Compare(tTimeData _tLeft, tTimeData _tRight)
        {
            return _tLeft.fPushTime.CompareTo(_tRight.fPushTime);
        }
    }

    [SerializeField] private SOPoolDataSet m_refStaticPoolDataSet;

    private readonly Dictionary<string, PoolEntry> m_hashPool = new Dictionary<string, PoolEntry>();
    private PriorityQueue<tTimeData> m_PQTimer;
    private bool m_bStaticPoolsLoaded;

    public static ObjectPoolManager m_Instance { get; private set; }

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(gameObject);
        m_PQTimer = new PriorityQueue<tTimeData>(new tExpireTimeComparer());
    }

    private void Start()
    {
        UpdateExpireQueue(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void OnDestroy()
    {
        if (m_Instance != this)
            return;

        ClearPool();
        m_Instance = null;
    }

    private async UniTaskVoid UpdateExpireQueue(CancellationToken _tToken)
    {
        while (true)
        {
            if (m_PQTimer.Count <= 0)
            {
                await UniTask.Yield(_tToken);
                continue;
            }

            var tTimeData = m_PQTimer.Peek();
            if (tTimeData.fPushTime - Time.time > 0.0f)
            {
                await UniTask.Yield(_tToken);
                continue;
            }

            m_PQTimer.Dequeue();

            if (tTimeData.refPoolObj != null && tTimeData.refPoolObj.Generation == tTimeData.iGeneration)
                PushObject(tTimeData.refPoolObj.gameObject);
        }
    }

    public void ScheduleTime(PoolObject _refPoolObj, float _fAliveTime)
    {
        m_PQTimer.Enqueue(new tTimeData(Time.time + _fAliveTime, _refPoolObj, _refPoolObj.Generation));
    }

    public async UniTask LoadStaticPoolDataAsync(CancellationToken _token = default, IProgress<float> _refProgress = null)
    {
        if (m_bStaticPoolsLoaded == true)
        {
            _refProgress?.Report(1.0f);
            return;
        }

        await LoadPoolsAsync(m_refStaticPoolDataSet.PoolDataList, true, _token, _refProgress);
        m_bStaticPoolsLoaded = true;
    }

    public async UniTask ReplaceScenePoolsAsync(IReadOnlyList<SOPoolData> _listPoolData, CancellationToken _token = default, IProgress<float> _refProgress = null)
    {
        ClearPoolEntries(false);
        await LoadPoolsAsync(_listPoolData, false, _token, _refProgress);
    }

    // 테스트 씬 호환 진입점. 상시 세트 없이 전달된 목록만 씬 수명으로 교체한다.
    public async UniTask LoadPoolAsync(IReadOnlyList<SOPoolData> _listPoolData, CancellationToken _token = default, IProgress<float> _refProgress = null)
    {
        SceneChange();
        await ReplaceScenePoolsAsync(_listPoolData, _token, _refProgress);
    }

    /// <summary>
    /// 현재 씬에 살아있는, 지워지지 말아야할 오브젝트는 풀에 반납 후 나머지 오브젝트 삭제
    /// </summary>
    public void SceneChange()
    {
        m_PQTimer.Clear();

        foreach (var tPair in m_hashPool)
        {
            PoolEntry refEntry = tPair.Value;
            if (refEntry.IsStatic == false)
                continue;

            for (int i = 0; i < refEntry.Instances.Count; ++i)
            {
                PoolObject refPoolObj = refEntry.Instances[i];
                if (refPoolObj != null && refPoolObj.PushCount == 0)
                    PushObject(refPoolObj.gameObject);
            }
        }

        ClearPoolEntries(false);
    }

    public void ClearPool()
    {
        if (m_PQTimer != null)
            m_PQTimer.Clear();

        ClearPoolEntries(null);
        m_bStaticPoolsLoaded = false;
    }

    private async UniTask LoadPoolsAsync(IReadOnlyList<SOPoolData> _listPoolData, bool _bStatic, CancellationToken _token, IProgress<float> _refProgress)
    {
        if (_listPoolData == null || _listPoolData.Count == 0)
        {
            _refProgress?.Report(1.0f);
            return;
        }

        var hashScheduledKey = new HashSet<string>();
        var listPoolData = new List<SOPoolData>(_listPoolData.Count);

        for (int i = 0; i < _listPoolData.Count; ++i)
        {
            SOPoolData refPoolData = _listPoolData[i];
            string strKey = GetKey(refPoolData);

            if (strKey == null || m_hashPool.ContainsKey(strKey) == true)
                continue;

            if (hashScheduledKey.Add(strKey) == true)
                listPoolData.Add(refPoolData);
        }

        if (listPoolData.Count == 0)
        {
            _refProgress?.Report(1.0f);
            return;
        }

        var refLoadProgress = new PoolLoadProgress(listPoolData.Count, _refProgress);
        var listTasks = new List<UniTask>(listPoolData.Count);

        for (int i = 0; i < listPoolData.Count; ++i)
            listTasks.Add(InstanceAsync(listPoolData[i], _bStatic, _token, refLoadProgress));

        await UniTask.WhenAll(listTasks);
    }

    private async UniTask InstanceAsync(SOPoolData _refData, bool _bStatic, CancellationToken _token, PoolLoadProgress _refLoadProgress)
    {
        AsyncOperationHandle<GameObject> tHandle = default;
        bool bEntryRegistered = false;

        try
        {
            if (_refData == null || _refData.PrefabRef == null || _refData.PrefabRef.RuntimeKeyIsValid() == false)
            {
                Debug.LogError("풀 프리팹 미설정 : ObjectPool");
                return;
            }

            tHandle = Addressables.LoadAssetAsync<GameObject>(_refData.PrefabRef);
            GameObject refPrefab = await tHandle.ToUniTask(cancellationToken: _token);
            PoolObject refPrefabPoolObj = refPrefab.GetComponent<PoolObject>();

            if (refPrefabPoolObj == null)
            {
                Debug.LogError("풀 프리팹에 PoolObject 없음 : " + _refData.name);
                return;
            }

            var tOpInstantiate = UnityEngine.Object.InstantiateAsync(refPrefab, _refData.PreLoad);
            GameObject[] arrInstance;

            using (_token.Register(() => tOpInstantiate.Cancel()))
                arrInstance = await tOpInstantiate.ToUniTask(cancellationToken: _token);

            var refEntry = new PoolEntry
            {
                IsStatic = _bStatic,
                Handle = tHandle,
                Prefab = refPrefabPoolObj,
                ActiveCap = _refData.ActiveCap
            };

            string strKey = GetKey(_refData);
            m_hashPool.Add(strKey, refEntry);
            bEntryRegistered = true;

            for (int i = 0; i < arrInstance.Length; ++i)
            {
                GameObject refInstance = arrInstance[i];
                PoolObject refInstancePoolObj = refInstance.GetComponent<PoolObject>();

                if (_bStatic == true)
                    refInstance.transform.SetParent(transform, true);

                refInstancePoolObj.SetPoolKey(_refData);
                refInstancePoolObj.Push();
                refInstance.SetActive(false);

                refEntry.Instances.Add(refInstancePoolObj);
                refEntry.Pool.Push(refInstance);
            }
        }
        finally
        {
            if (bEntryRegistered == false && tHandle.IsValid() == true)
                Addressables.Release(tHandle);

            _refLoadProgress.CompleteOne();
        }
    }

    private void ClearPoolEntries(bool? _bStatic)
    {
        var listRemoveKey = new List<string>();

        foreach (var tPair in m_hashPool)
        {
            PoolEntry refEntry = tPair.Value;
            if (_bStatic.HasValue == true && refEntry.IsStatic != _bStatic.Value)
                continue;

            for (int i = 0; i < refEntry.Instances.Count; ++i)
            {
                PoolObject refPoolObj = refEntry.Instances[i];
                if (refPoolObj != null)
                    Destroy(refPoolObj.gameObject);
            }

            if (refEntry.Handle.IsValid() == true)
                Addressables.Release(refEntry.Handle);

            listRemoveKey.Add(tPair.Key);
        }

        for (int i = 0; i < listRemoveKey.Count; ++i)
            m_hashPool.Remove(listRemoveKey[i]);
    }

    private static string GetKey(SOPoolData _refPoolData)
    {
        if (_refPoolData == null || _refPoolData.PrefabRef == null)
            return null;

        return _refPoolData.PrefabRef.AssetGUID;
    }

    public PoolObject GetPoolPrefab(SOPoolData _refPoolData)
    {
        string strKey = GetKey(_refPoolData);
        if (strKey == null || m_hashPool.TryGetValue(strKey, out PoolEntry refEntry) == false)
            return null;

        return refEntry.Prefab;
    }

    public GameObject GetObject(SOPoolData _refPoolData)
    {
        string strKey = GetKey(_refPoolData);
        if (strKey == null || m_hashPool.TryGetValue(strKey, out PoolEntry refEntry) == false)
            return null;

        if (refEntry.ActiveCap > 0 && refEntry.ActiveInstances.Count >= refEntry.ActiveCap)
            PushObject(refEntry.ActiveInstances.First.Value);

        while (refEntry.Pool.Count > 0)
        {
            GameObject refObject = refEntry.Pool.Pop();
            if (refObject == null)
                continue;

            IPoolable iPool = refObject.GetComponent<IPoolable>();
            if (iPool == null)
            {
                Debug.LogError("오브젝트 풀에 IPoolable이 없는 인스턴스가 있음");
                continue;
            }

            iPool.Pop();
            refObject.SetActive(true);

            if (refEntry.ActiveCap > 0)
                refEntry.ActiveInstances.AddLast(refObject);

            return refObject;
        }

        return null;
    }

    public GameObject GetObject(SOPoolData _refPoolData, Vector3 _vSpawnPos)
    {
        GameObject refObj = GetObject(_refPoolData);
        if (refObj == null)
            return null;

        refObj.transform.position = _vSpawnPos;
        return refObj;
    }

    public void PushObject(GameObject _refGameObj)
    {
        PoolObject refPoolObj = _refGameObj.GetComponent<PoolObject>();
        if (refPoolObj == null)
            return;

        string strKey = GetKey(refPoolObj.PoolKey);
        if (strKey == null || m_hashPool.TryGetValue(strKey, out PoolEntry refEntry) == false)
            return;

        if (refPoolObj.PushCount > 0)
            return;

        refPoolObj.Push();
        _refGameObj.SetActive(false);
        refEntry.Pool.Push(_refGameObj);

        if (refEntry.ActiveCap > 0)
            refEntry.ActiveInstances.Remove(_refGameObj);
    }

    public int GetObjectCount(SOPoolData _refPoolData)
    {
        string strKey = GetKey(_refPoolData);
        if (strKey == null || m_hashPool.TryGetValue(strKey, out PoolEntry refEntry) == false)
            return -1;

        return refEntry.Pool.Count;
    }
}
