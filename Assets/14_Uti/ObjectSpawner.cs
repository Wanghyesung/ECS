using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    private PriorityQueue<tSpawnData> m_PQObject;
    private struct tSpawnData
    {
        public float fSpawnTime;
        public PoolObject refSpawnObject;
        public Vector3 vPosition;

        public tSpawnData(float _fTime, PoolObject _refSpawnObj, Vector3 _vPosition)
        {
            fSpawnTime = _fTime;
            refSpawnObject = _refSpawnObj;
            vPosition = _vPosition;
        }
    }
    private struct tSpawnTimeComparer : IComparer<tSpawnData>
    {
        public int Compare(tSpawnData x, tSpawnData y)
        {
            return x.fSpawnTime.CompareTo(y.fSpawnTime);
        }
    }

    private void Awake()
    {
        m_PQObject = new PriorityQueue<tSpawnData>(new tSpawnTimeComparer());
    }

    private void Start()
    {
        //이 비동기 작업의 결과를 안 기다리고 그냥 던진다
        UpdateSpawnObject(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid UpdateSpawnObject(CancellationToken _tToken)
    {
       while(true)
       {
            if(m_PQObject.Count <= 0)
            {
                await UniTask.Yield(_tToken);
                continue;
            }

            //가장 빨리 나오는 오브젝트만 확인
            var tSpawn = m_PQObject.Peek();
            if (tSpawn.fSpawnTime - Time.time > 0.0f)
            {
                await UniTask.Yield(_tToken);
                continue;
            }

            m_PQObject.Dequeue();
            SpawnObject(tSpawn.refSpawnObject, tSpawn.vPosition);
       }
    }

    public void AddSpawnObject(float _fNextSpawnTime, PoolObject _refPoolData, Vector3 _vPosition = default)//default를 쓰면 컴파일 타임 상수로
    {
        tSpawnData tData = new tSpawnData(Time.time + _fNextSpawnTime, _refPoolData, _vPosition);
        m_PQObject.Enqueue(tData);
    }

    private void SpawnObject(PoolObject _refSpawnObject, Vector3 _vPosition)
    {
        GameObject refGameObject = ObjectPool.m_Instance.GetObject(_refSpawnObject);
        if (refGameObject == null)
            return;

        refGameObject.transform.position = _vPosition;
    }
}
