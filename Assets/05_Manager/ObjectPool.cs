using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;
using static PoolObject;


[Serializable]
public class PoolInfo
{
    public int iPoolCount;
    public GameObject refGameObject;
}


public class ObjectPool : MonoBehaviour
{
    public static ObjectPool m_Instance = null; 
    private Dictionary<ePoolType, Queue<GameObject>> m_hashPool = new Dictionary<ePoolType, Queue<GameObject>>();

    //임시로 넣은 게임 오브젝트
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
        //todo : 중복체크
        for(int i = 0; i<m_listPoolObject.Count; ++i)
        {
            PoolInfo refPool = m_listPoolObject[i];

            GameObject refObj = refPool.refGameObject;
            IPoolable iPoolAble = refObj.GetComponent<IPoolable>();
            if(iPoolAble == null)
            {
                Debug.Log("이상한 값이 들어옴 : ObjectPool");
                return;
            }

            
            Queue<GameObject> queGameObject = new Queue<GameObject>();
            //같은 값 들어오면 터트리기
            m_hashPool.Add(iPoolAble.PoolType, queGameObject);

            for (int j = 0; j<refPool.iPoolCount; ++j)
            {
                GameObject refInstance = Instantiate(refObj);
                PushObject(refInstance);
            }
        }
    }

    public GameObject GetObject(ePoolType _ePoolType)
    {
        if (m_hashPool.TryGetValue(_ePoolType, out var queValue) == false)
            return null;

        if (queValue.Count == 0)
            return null;

        GameObject refObject = queValue.Dequeue();
        IPoolable iPool = refObject.GetComponent<IPoolable>();
        if (iPool == null)
            return null;

        refObject.transform.SetParent(null);

        iPool.Pop();
        refObject.gameObject.SetActive(true);

        queValue.Dequeue();
        return refObject;
    }


    //public GameObject GetObject(ePoolType _ePoolType, in Vector3 _vWorldPos, in Quaternion _qRotate)
    //{
    //    GameObject refObj = GetObject(_ePoolType);
    //
    //    //여기서 바꾸기
    //}

    public void PushObject(GameObject _refGameObj)
    {
        IPoolable iPool = _refGameObj.GetComponent<IPoolable>();
        if (iPool == null)
            return;

        if (m_hashPool.TryGetValue(iPool.PoolType, out var queValue) == false)
            return;

        _refGameObj.transform.SetParent(transform);

        iPool.Push();
        _refGameObj.gameObject.SetActive(false);

        queValue.Enqueue(_refGameObj);
    }

}
