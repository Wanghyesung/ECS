using System;
using UnityEngine;
using UnityEngine.Events;

public interface IPoolable
{
    public PoolObject PoolKey { get; }
    public int PushCount { get; }

    public void SetOriginalPoolObj(PoolObject _refOriginObj);
    public void Push();
    public void Pop();
}

public class PoolObject : MonoBehaviour, IPoolable
{
    private PoolObject m_refOriginalPoolObj;

    private int m_iPushCount = 0;
    public int PushCount { get { return m_iPushCount; } }
    public PoolObject PoolKey { get { return m_refOriginalPoolObj; } }

    public event Action OnPush;
    public event Action OnPop;


    [SerializeField] private float m_fAliveTime = 3.0f;
    private float m_fSettingAliveTime = 0.0f;

    private void Update()
    {
        m_fAliveTime -= Time.deltaTime;
        if (m_fAliveTime <= 0.0f)
            ObjectPool.m_Instance.PushObject(gameObject);
    }

 
    public virtual void Push()
    {
        m_iPushCount = 1;
        OnPush?.Invoke();
    }

    public virtual void Pop()
    {
        m_iPushCount = 0;
        m_fAliveTime = m_fSettingAliveTime > 0f ? m_fSettingAliveTime : float.MaxValue;
        OnPop?.Invoke();
    }

    public void SetAliveTime(float _fPushTime)
    {
        m_fAliveTime = _fPushTime;
        m_fSettingAliveTime = m_fAliveTime;
    }

    public void SetOriginalPoolObj(PoolObject _refOriginObj)
    {
        m_refOriginalPoolObj = _refOriginObj;
    }
}
