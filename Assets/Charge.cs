using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/*///////////////////////////////////////////
               Charge
기능 : 몬스터가 공격 오브젝트를 소환하는 기능
 *///////////////////////////////////////////

public class Charge : MonoBehaviour
{
    [SerializeField] private ParticleSystem m_refParticleSystem;

    private Action OnChargeComplete;
    private float m_fTargetTime;
    private float m_fCurrentTime;

    public void StartCharge(float _fDuration, params Action[] _arrCallbacks)
    {
        if (_arrCallbacks == null)
        {
            return;
#if UNITY_EDITOR
            Debug.Log("파라미터 넣어줘야함");
#endif
        }

        m_refParticleSystem.Play();

        ClearAllEvents();

        m_fTargetTime = _fDuration;
        m_fCurrentTime = 0f;

        for (int i = 0; i < _arrCallbacks.Length; i++)
           OnChargeComplete += _arrCallbacks[i];

    }

    private void Update()
    {
        m_fCurrentTime += Time.deltaTime;
        if (m_fCurrentTime >= m_fTargetTime)
        {

            OnChargeComplete?.Invoke();

            ClearAllEvents();
        }
    }

    public void ClearAllEvents()
    {
        OnChargeComplete = null; 
    }

}
