using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class OnCompletedParticle: MonoBehaviour
{
    [SerializeField] private ParticleSystem m_refParticle;
    [SerializeField] private UnityEvent m_refCompletedAction;

    private void Awake()
    {
        m_refParticle = GetComponent<ParticleSystem>();
    }
    private void OnEnable()
    {
        m_refParticle?.Play();
    }

    private void OnParticleSystemStopped()
    {
        m_refCompletedAction?.Invoke();
    }
}
