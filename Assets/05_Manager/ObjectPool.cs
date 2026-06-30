using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PoolObject;


//해당 데이터에 접근하여 수정할 일이 없다면 struct로 교체
[Serializable]
public class PoolInfo
{
    public int iPoolCount;
    public GameObject refGameObject;
}

[Serializable]
public enum ePoolType
{
    /*Player*/
    None,

    BaseBullet,
    BaseHitEffect,
    MidBullet,
    MidHitEffect,
    Missile,
    LargeAttackEx,

    /*Monster*/
    GBossBall,
    GBossBallEffect,

    LightBall,
    LightBallEffect,

    MonBullet,
    SparkEx,

    BossLaser,

    BossMissile,
    BossMissileEx,
}

/*///////////////////////////////////////////
               ObjectPool
기능 : 객체를 미리 로드해두고 필요할 때 꺼내고 다 사용하면 반납할 수 있게 하는 기능
 *///////////////////////////////////////////


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
        {
            Debug.Log("오브젝트 풀에 이상한 값이 들어감");
            return null;
        }

        refObject.transform.SetParent(null);

        iPool.Pop();
        refObject.gameObject.SetActive(true);

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

        //이미 푸쉬 요청된 오브젝트
        if (iPool.PushCount > 0)
            return;

        iPool.Push();
        _refGameObj.transform.SetParent(transform);
        _refGameObj.gameObject.SetActive(false);

        queValue.Enqueue(_refGameObj);
    }

    public int GetObjectCount(ePoolType _eType)
    {
        if (m_hashPool.TryGetValue(_eType, out var queValue) == false)
            return -1;

        return queValue.Count;
    }
}
