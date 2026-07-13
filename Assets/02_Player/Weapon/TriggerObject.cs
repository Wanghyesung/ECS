using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static PoolObject;

public class TriggerObject : MonoBehaviour
{
    public event Action<Collider> OnHitTargetEnter; //피격 이벤트
    [SerializeField] private UnityEvent OnHitEvent; //충돌 이벤트

    [SerializeField] private LayerMask m_tHitLayer; 
   
    protected virtual void OnTriggerEnter(Collider other)
    {
        if ((m_tHitLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            OnHitTargetEnter?.Invoke(other);

            OnHitEvent?.Invoke();
        }
    }

    public void SetTriggerMask(LayerMask _tLayerMask) { m_tHitLayer = _tLayerMask; }
}
