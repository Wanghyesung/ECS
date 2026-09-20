using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.Events;
using static PoolObject;


public interface ITriggerable
{
    public Observable<Collider> OnHitTargetEnter { get; }
    public LayerMask LayerMask { get; set; }
}

public class TriggerEnterObject : MonoBehaviour, ITriggerable
{
    private readonly Subject<Collider> m_subjectEnter = new();
    public Observable<Collider> OnHitTargetEnter => m_subjectEnter;

    [SerializeField] private UnityEvent OnHitEvent; //충돌 이벤트

    [SerializeField] private LayerMask m_tHitLayer;

    public LayerMask LayerMask
    {
        get { return m_tHitLayer; }
        set { m_tHitLayer = value; }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if ((m_tHitLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            m_subjectEnter.OnNext(other);

            OnHitEvent?.Invoke();
        }
    }

}
