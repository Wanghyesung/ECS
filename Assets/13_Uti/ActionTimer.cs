using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionTimer : MonoBehaviour
{
    [SerializeField] private event Action OnCompletedTime;
    [SerializeField] private float m_fActionTime;
    private float m_fCurTime = 0.0f;


    private void Update()
    {
        m_fCurTime += Time.deltaTime;
        if (m_fCurTime >= m_fActionTime)
        {
            m_fCurTime = 0.0f;
            OnCompletedTime?.Invoke();
        }
    }
}
