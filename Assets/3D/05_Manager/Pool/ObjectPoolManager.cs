using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/*///////////////////////////////////////////
               ObjectPool
기능 : 오브젝트를 미리 로드해두고 필요할 때 꺼내어 쓰면 반납할 수 있게 하는 클래스
       SOSceneData -> SOPoolData 목록을 외부(SceneController)에서 받아
       Addressables 비동기 로드 + UniTask 프레임 분산으로 프리워밍한다.
 *///////////////////////////////////////////

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager m_Instance = null;
    // 재사용 대기열은 Stack(LIFO) - 방금 반납된 것부터 다시 꺼내 쓴다
    private Dictionary<PoolObject, Stack<GameObject>> m_hashPool = new Dictionary<PoolObject, Stack<GameObject>>();
    private Dictionary<PoolObject, AsyncOperationHandle> m_hashHandle = new Dictionary<PoolObject, AsyncOperationHandle>();

    // SOPoolData -> 그 데이터로 Addressables 로드해 등록한 PoolObject 키. 직접 참조(SOStage.MonsterPrefab 등)로
    // 같은 프리팹을 또 들고 있으면 빌드에서는 별개의 인스턴스가 되어 m_hashPool 조회가 항상 실패하므로,
    // 풀을 요청하는 쪽은 전부 이 SOPoolData를 거쳐서 실제 등록된 키를 얻어야 한다.
    private Dictionary<SOPoolData, PoolObject> m_hashPoolDataKey = new Dictionary<SOPoolData, PoolObject>();

    // 동시 활성 개수 상한이 걸린 PoolObject만 등록됨(SOPoolData.ActiveCap > 0). 없으면 상한 없음(기존 동작과 동일)
    private Dictionary<PoolObject, int> m_hashActiveCap = new Dictionary<PoolObject, int>();

    // LinkedList를 쓰는 이유: 활성 인스턴스는 상한 초과(맨 앞 강제 반납)뿐 아니라 자연 만료나
    // 게임 로직의 수동 PushObject 호출로도 "중간에서" 빠질 수 있다.
    private Dictionary<PoolObject, LinkedList<GameObject>> m_hashActiveList = new Dictionary<PoolObject, LinkedList<GameObject>>();

    // PoolObject별 알아서 매 프레임 카운트다운하는 대신, "이 시각에 반납"만 예약해두고
    // 이 매니저가 큐 맨 앞(가장 이른 만료 시각)만 확인하는 방식 (ObjectSpawner와 동일한 패턴)
    private PriorityQueue<tTimeData> m_PQTimer;

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
        public int Compare(tTimeData x, tTimeData y)
        {
            return x.fPushTime.CompareTo(y.fPushTime);
        }
    }

    private int m_iLoadCount = 0;
    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(this);

        m_PQTimer = new PriorityQueue<tTimeData>(new tExpireTimeComparer());
    }

    private void Start()
    {
        UpdateExpireQueue(this.GetCancellationTokenOnDestroy()).Forget();
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

            // 가장 이른 만료 시각만 확인
            var tTimeData = m_PQTimer.Peek();
            if (tTimeData.fPushTime - Time.time > 0.0f)
            {
                await UniTask.Yield(_tToken);
                continue;
            }

            m_PQTimer.Dequeue();

            // 예약 이후 수동 Push -> 재사용(Pop)됐으면 Generation이 달라져 있음 - 낡은 예약이라 무시
            if (tTimeData.refPoolObj.Generation == tTimeData.iGeneration)
                PushObject(tTimeData.refPoolObj.gameObject);
        }
    }

    // PoolObject.Pop()/SetAliveTime()에서 호출 - 지정한 시간 뒤 자동으로 풀에 반납되도록 예약.
    // 0 이하를 넘기면 "즉시 반납"(다음 체크 때 바로 처리)으로 취급 - "자동 예약 안 함"이
    // 필요한 경우(Pop()의 기본값 폴백)는 호출부에서 걸러줌
    public void ScheduleTime(PoolObject _refPoolObj, float _fAliveTime)
    {
        m_PQTimer.Enqueue(new tTimeData(Time.time + _fAliveTime, _refPoolObj, _refPoolObj.Generation));
    }

    //_refProgress: 풀 프리팹 하나 완료될 때마다 (완료 개수 / 전체 개수)를 보고 (0~1). 로딩 화면 진행바용
    public async UniTask LoadPoolAsync(List<SOPoolData> _listPoolData, CancellationToken _token = default, IProgress<float> _refProgress = null)
    {
        m_iLoadCount = 0;

        ClearPool();

        if (_listPoolData == null || _listPoolData.Count == 0)
        {
            _refProgress?.Report(1.0f);
            return;
        }

        int iTotalCount = _listPoolData.Count;

        var listTasks = new List<UniTask>(iTotalCount); // <- 풀링으로 변경
        for (int i = 0; i < iTotalCount; ++i)
            listTasks.Add(IntanceAsync(_listPoolData[i], _token, iTotalCount, _refProgress));

        await UniTask.WhenAll(listTasks);
    }

    private async UniTask IntanceAsync(SOPoolData _refData, CancellationToken _token, int _iTotalCount, IProgress<float> _refProgress)
    {
        if (_refData == null || _refData.PrefabRef == null || _refData.PrefabRef.RuntimeKeyIsValid() == false)
        {
            Debug.Log("풀 프리팹 미설정 : ObjectPool");
            return;
        }

        var tHandle = Addressables.LoadAssetAsync<GameObject>(_refData.PrefabRef);

        GameObject refPrefab = await tHandle.ToUniTask(cancellationToken: _token, autoReleaseWhenCanceled: true);

        PoolObject refPrefabPoolObj = refPrefab.GetComponent<PoolObject>();
        if (refPrefabPoolObj == null)
        {
            Debug.Log("풀 프리팹에 PoolObject 없음 : ObjectPool");
            Addressables.Release(tHandle);
            return;
        }

        m_hashHandle[refPrefabPoolObj] = tHandle;
        m_hashPoolDataKey[_refData] = refPrefabPoolObj;

        Stack<GameObject> stackGameObject = new Stack<GameObject>();
        m_hashPool[refPrefabPoolObj] = stackGameObject;

        if (_refData.ActiveCap > 0)
        {
            m_hashActiveCap[refPrefabPoolObj] = _refData.ActiveCap;
            m_hashActiveList[refPrefabPoolObj] = new LinkedList<GameObject>();
        }

        var tOpInstantiate = UnityEngine.Object.InstantiateAsync(refPrefab, _refData.PreLoad);
        GameObject[] arrInstance;

        //var t =tOpInstantiate.Result;//tOp.Result — 생성이 끝날 때까지 스레드를 붙잡음. 그동안 아무것도 못 함. 끊김.
        //이 토큰이 취소되는 순간 이 콜백을 실행 , using으로 감싸면 블록을 빠져나갈 때 자동으로 등록이 해제(Dispose)
       
        using (_token.Register(() => tOpInstantiate.Cancel()))
        {
            arrInstance = await tOpInstantiate.ToUniTask(cancellationToken: _token);
        }

        for (int i = 0; i < arrInstance.Length; ++i)
        {
            PoolObject refInstancePoolObj = arrInstance[i].GetComponent<PoolObject>();
            refInstancePoolObj.SetOriginalPoolObj(refPrefabPoolObj);
            PushObject(arrInstance[i]);
        }

        ++m_iLoadCount;
        _refProgress?.Report((float)m_iLoadCount / _iTotalCount);
    }

    public void ClearPool()
    {
        foreach (var kvValue in m_hashPool)
        {
            Stack<GameObject> stackValue = kvValue.Value;
            while (stackValue.Count > 0)
            {
                GameObject refObj = stackValue.Pop();
                if (refObj != null)
                    Destroy(refObj);
            }
        }
        m_hashPool.Clear();

        foreach (var tKvHandle in m_hashHandle)
            Addressables.Release(tKvHandle.Value);
        m_hashHandle.Clear();

        m_hashActiveCap.Clear();
        m_hashActiveList.Clear();
        m_hashPoolDataKey.Clear();
    }

    // SOStage 등, 프리팹을 직접 참조로 들고 있지 않고 SOPoolData로 요청하는 호출부용.
    public PoolObject GetPoolPrefab(SOPoolData _refPoolData)
    {
        if (_refPoolData == null)
            return null;

        m_hashPoolDataKey.TryGetValue(_refPoolData, out var refPoolObj);
        return refPoolObj;
    }

    public GameObject GetObject(PoolObject _refPrefabPoolObj)
    {
        if (_refPrefabPoolObj == null)
            return null;

        if (m_hashPool.TryGetValue(_refPrefabPoolObj, out var stackValue) == false)
            return null;

        // 동시 개수 상한 - 여유가 없으면 가장 오래된 활성 인스턴스를 강제로 반납해 자리를 만든다
        m_hashActiveList.TryGetValue(_refPrefabPoolObj, out var listActive);
        if (listActive != null && listActive.Count >= m_hashActiveCap[_refPrefabPoolObj])
            PushObject(listActive.First.Value);

        if (stackValue.Count == 0)
            return null;

        GameObject refObject = stackValue.Pop();
        IPoolable iPool = refObject.GetComponent<IPoolable>();
        if (iPool == null)
        {
            Debug.Log("오브젝트 풀에 이상한 오류 있음");
            return null;
        }

        //refObject.transform.SetParent(null);
        iPool.Pop();
        refObject.gameObject.SetActive(true);

        listActive?.AddLast(refObject);

        return refObject;
    }

    public GameObject GetObject(PoolObject _refPrefabPoolObj, Vector3 _vSpawnPos)
    {
        GameObject refObj = GetObject(_refPrefabPoolObj);
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

        if (m_hashPool.TryGetValue(refPoolObj.PoolKey, out var stackValue) == false)
            return;

        if (refPoolObj.PushCount > 0)
            return;

        refPoolObj.Push();
        //_refGameObj.transform.SetParent(transform);
        _refGameObj.gameObject.SetActive(false);

        stackValue.Push(_refGameObj);

        // 반납 경로(상한 강제 반납/자연 만료/게임 로직의 수동 호출)와 무관하게 항상 여기서 활성
        // 목록에서 즉시 빠짐 - LinkedList라 위치에 상관없이 O(n) 탐색만으로 바로 제거 가능
        if (m_hashActiveList.TryGetValue(refPoolObj.PoolKey, out var listActive))
            listActive.Remove(_refGameObj);
    }

    public int GetObjectCount(PoolObject _refPrefabPoolObj)
    {
        if (m_hashPool.TryGetValue(_refPrefabPoolObj, out var stackValue) == false)
            return -1;

        return stackValue.Count;
    }
}
