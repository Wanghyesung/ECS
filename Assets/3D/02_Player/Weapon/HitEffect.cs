using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PoolObject))]
public class HitEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem m_refParticle;

    [Header("Size Scaling")]
    [SerializeField] private float m_fReferenceSize = 10f; // 이 크기의 몬스터가 죽으면 원본(1배) 스케일로 재생
    [SerializeField] private float m_fMinSizeRatio = 0.5f;
    [SerializeField] private float m_fMaxSizeRatio = 3f;

    private Vector3 m_vBaseScale;

    private void Awake()
    {
        m_refParticle = GetComponent<ParticleSystem>();
        m_vBaseScale = transform.localScale;
    }
    private void OnEnable()
    {
        transform.localScale = m_vBaseScale;
        if (m_refParticle != null)
            m_refParticle.Play();
    }

    // 몬스터의 Renderer bounds 크기(_fSize)에 비례해 이펙트 크기 조정.
    // 풀에서 재사용돼도 매번 원본 스케일(m_vBaseScale) 기준으로 다시 계산하므로 누적 오차 없음
    public void SetSize(float _fSize)
    {
        if (m_fReferenceSize <= 0f)
            return;

        float fRatio = Mathf.Clamp(_fSize / m_fReferenceSize, m_fMinSizeRatio, m_fMaxSizeRatio);
        transform.localScale = m_vBaseScale * fRatio;
    }

    // 몬스터 사망 연출은 메시 크기와 무관하게 SO에서 지정한 배율을 사용한다.
    public void SetScale(float _fScale)
    {
        transform.localScale = m_vBaseScale * Mathf.Max(0.1f, _fScale);
        if (m_refParticle != null)
        {
            m_refParticle.Clear(true);
            m_refParticle.Play(true);
        }
    }

    private void OnParticleSystemStopped()
    {
        ObjectPoolManager.m_Instance.PushObject(gameObject);
    }
}
