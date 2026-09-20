using System;
using System.Collections;
using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.Events;

public class TriggerStayObject : MonoBehaviour, ITriggerable
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

    protected virtual void OnTriggerStay(Collider other)
    {
        if ((m_tHitLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            m_subjectEnter.OnNext(other);

            OnHitEvent?.Invoke();
        }
    }
}
