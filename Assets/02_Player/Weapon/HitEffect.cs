using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem m_refParticle;

    private void Awake()
    {
        m_refParticle = GetComponent<ParticleSystem>();
    }
    private void OnEnable()
    {
        m_refParticle?.Play();
    }

    //ฤÝน้
    private void OnParticleSystemStopped()
    {
        ObjectPool.m_Instance.PushObject(gameObject);
    }
}
