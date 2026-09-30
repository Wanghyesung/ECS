using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.ParticleSystem;
using static SoundManager;


/*///////////////////////////////////////////
               Charge
목적 : 일정 시간동안 차지 파티클 실행, 쿨타임 대기
 *///////////////////////////////////////////

public class Charge : MonoBehaviour
{
    [SerializeField] private ParticleSystem m_refParticleSystem;

    [SerializeField] private UnityEvent OnChargeComplete;
    [SerializeField] private SOAudio m_refChargeSound;
    [SerializeField] private float m_fStartEventOffset;

    private SoundHandle m_tSoundHandle;
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
        m_tSoundHandle = SoundManager.m_Instance.PlaySfx(m_refChargeSound);
        m_refParticleSystem.Play();
    }

    public void StopCharge()
    {
        if (m_refParticleSystem == null)
            return;

        SoundManager.m_Instance.StopSfx(m_tSoundHandle);
        m_refParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        m_bCompleted = true;
    }

    private void OnDisable()
    {
        SoundManager.m_Instance.StopSfx(m_tSoundHandle);
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
