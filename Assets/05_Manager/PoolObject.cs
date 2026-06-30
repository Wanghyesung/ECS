using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ObjectPool;

public interface IPoolable
{
    public ePoolType PoolType { get; }
    public int PushCount { get; }

    public void Push();
    public void Pop();
}

public class PoolObject : MonoBehaviour, IPoolable
{
    [SerializeField] private ePoolType m_ePoolType;

    private int m_iPushCount = 0;
    public int PushCount { get { return m_iPushCount; } }
    public ePoolType PoolType { get { return m_ePoolType; } }

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
}
