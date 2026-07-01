using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PoolInfo
{
    public int iPoolCount;
    public GameObject refGameObject;
}

/*///////////////////////////////////////////
               ObjectPool
목적 : 오브젝트를 미리 로드해두고 필요할 때 꺼내어 쓰면 반납할 수 있게 하는 클래스
 *///////////////////////////////////////////

public class ObjectPool : MonoBehaviour
{
    public static ObjectPool m_Instance = null;
    private Dictionary<PoolObject, Queue<GameObject>> m_hashPool = new Dictionary<PoolObject, Queue<GameObject>>();

    [SerializeField] private List<PoolInfo> m_listPoolObject = new List<PoolInfo>();

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(this);

        m_Instance = this;
        DontDestroyOnLoad(this);
    }

    private void Start()
    {
        for (int i = 0; i < m_listPoolObject.Count; ++i)
        {
            PoolInfo refPool = m_listPoolObject[i];
            PoolObject refPrefabPoolObj = refPool.refGameObject.GetComponent<PoolObject>();

            if (refPrefabPoolObj == null)
            {
                Debug.Log("풀 프리팹 미설정 : ObjectPool");
                return;
            }
            
            Queue<GameObject> queGameObject = new Queue<GameObject>();
            m_hashPool.Add(refPrefabPoolObj, queGameObject);

            for (int j = 0; j < refPool.iPoolCount; ++j)
            {
                GameObject refInstance = Instantiate(refPrefabPoolObj.gameObject);
                PoolObject instancePoolObj = refInstance.GetComponent<PoolObject>();
                instancePoolObj.SetOriginalPoolObj(refPrefabPoolObj);
                PushObject(refInstance);
            }
        }
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
