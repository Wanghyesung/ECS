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
    [SerializeField] private PoolObject m_refOriginalPoolObj;

    [SerializeField] private int m_iPushCount = 0;
    public int PushCount { get { return m_iPushCount; } }
    public PoolObject PoolKey { get { return m_refOriginalPoolObj; } }

    public event Action OnPush;
    public event Action OnPop;


    [SerializeField] private float m_fAliveTime = 3.0f;

    // 이 오브젝트가 몇 번째 생애(Pop)인지. ObjectPool의 만료 예약(PriorityQueue)이 "이전
    // 생애의 낡은 예약"인지 구분하는 용도 - 예약 이후 중간에 수동 Push되고 다시 Pop돼도
    // 낡은 예약이 새 생애를 잘못 반납시키지 않도록 막아줌 (SetAliveTime 재호출 시에도 증가)
    public int Generation { get; private set; } = 0;

    public virtual void Push()
    {
        m_iPushCount = 1;
        OnPush?.Invoke();
    }

    public virtual void Pop()
    {
        m_iPushCount = 0;
        ++Generation;
        OnPop?.Invoke();

        // SetAliveTime을 따로 안 부르는 오브젝트(예: 히트 이펙트)도 프리팹 기본 m_fAliveTime으로
        // 자동 반납되도록 일단 예약해둠. 이후 SetAliveTime이 호출되면 Generation이 다시 올라가
        // 이 예약은 자동으로 무효화되고 새 시간으로 재예약됨. 0 이하로 세팅된 프리팹은
        // "수동으로만 반납한다"는 의도로 보고 자동 예약을 안 함
        if (m_fAliveTime > 0f)
            ObjectPoolManager.m_Instance.ScheduleTime(this, m_fAliveTime);
    }

    // 매 프레임 직접 카운트다운하지 않고, ObjectPool의 우선순위 큐에 "이 시각에 반납"으로
    // 예약만 해둔다. ObjectPool은 큐 맨 앞(가장 이른 만료 시각)만 매 프레임 확인함.
    public void SetAliveTime(float _fPushTime)
    {
        m_fAliveTime = _fPushTime;
        ++Generation;
        ObjectPoolManager.m_Instance.ScheduleTime(this, _fPushTime);
    }

    public void SetOriginalPoolObj(PoolObject _refOriginObj)
    {
        m_refOriginalPoolObj = _refOriginObj;
    }
}
