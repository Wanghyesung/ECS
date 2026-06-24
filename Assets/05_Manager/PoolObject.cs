using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public interface IPoolable
{
    public PoolObject.ePoolType PoolType { get; }
    public int PushCount { get; }

    public void Push();
    public void Pop();
}

public class PoolObject : MonoBehaviour, IPoolable
{
    [SerializeField] ePoolType m_ePoolType;

    //만약 이번 프레임에서 2번 이상 오브젝트를 Push할 수 있기 때문에
    private int m_iPushCount = 0;
    public int PushCount { get { return m_iPushCount; } }
    public PoolObject.ePoolType PoolType { get { return m_ePoolType; } }

    [SerializeField] private float m_fAliveTime = 3.0f;
    private float m_fReturnTime = 0.0f;
    public float ReturnTime { get { return m_fReturnTime; } }

    [Serializable]
    public enum ePoolType
    {
        /*Player*/
        None,
        BaseBullet,
        BaseHitEffect,
        MidBullet,
        MidHitEffect,
        Missiles,
        LargeHitEffect,

        /*Monster*/
        GBossBall,
        GBossBallEx,
    }

    private void Update()
    {
        //이거 바꾸기 <- 우선순위 큐를 사용해서 가장 시간이 적은 얘만 확인하기
        m_fAliveTime -= Time.deltaTime;  
        if(m_fAliveTime <= 0.0f)
            ObjectPool.m_Instance.PushObject(gameObject);
    }
    public virtual void Push()
    {
        m_iPushCount = 1;
    }
    public virtual void Pop()
    {
        m_iPushCount = 0;

        //TODO : 이거 나중에 우선순위큐를 사용해서 가장 시간이 작은 얘만 검사하기
        //m_fReturnTime = Time.time + m_fAliveTime;
    }
    
    public void SetPushTime(float _fPushTime)
    {
        m_fAliveTime = _fPushTime;
    }
}
