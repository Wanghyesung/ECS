using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.ParticleSystem;



/*///////////////////////////////////////////
               Charge
목적 : 일정 시간동안 차지 파티클 실행, 쿨타임 대기
 *///////////////////////////////////////////

public class Charge : MonoBehaviour
{
    [SerializeField] private ParticleSystem m_refParticleSystem;

    [SerializeField] private UnityEvent OnChargeComplete;
    [SerializeField] private float m_fStartEventOffset;

    private float m_fStartEventTime;
    private float m_fCurTime = 0.0f;

    private bool m_bCompleted = false;

    private SpawnInfo m_refSpawnInfo = null;
    private void Awake()
    {
        if(m_refParticleSystem == null)
            m_refParticleSystem = GetComponent<ParticleSystem>();
    }

    public SpawnInfo SpawnInfo
    {
        get => m_refSpawnInfo;
        set => m_refSpawnInfo = value;
    }
    public void StartCharge(float _fDuration)
    {
        if (m_refParticleSystem == null)
            return;

        m_bCompleted = false;
        m_fStartEventTime = _fDuration;
        m_fStartEventTime += m_fStartEventOffset;
        m_fCurTime = m_fStartEventTime;

        //var mainModule = m_refParticleSystem.main;
        //mainModule.startLifetime = new ParticleSystem.MinMaxCurve(_fDuration);

        m_refParticleSystem.Play();
    }

    public void StopCharge()
    {
        if (m_refParticleSystem == null)
            return;

        m_refParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        m_bCompleted = true;
    }

    public void Update()
    {
        if (m_bCompleted == true)
            return;

        m_fCurTime -= Time.deltaTime;
        if(m_fCurTime <= 0.0f)
        {
            OnChargeComplete?.Invoke();
            m_bCompleted = true;
        }
    }
}
