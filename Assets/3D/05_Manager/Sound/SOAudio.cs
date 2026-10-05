using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/*///////////////////////////////////////////
                    SOAudio
목적 : 소리 한 종류의 재생 데이터 (클립 변형, 채널, 볼륨, 피치 범위, 3D 정도, 연사 간격, 우선순위, 루프)
       런타임 상태(마지막 재생 시각 등)는 SoundManager가 소유 - 여러 무기/몬스터가 같은 SO를 공유해도 오염되지 않음
 *///////////////////////////////////////////
[CreateAssetMenu(menuName = "Audio/SOAudio")]
public sealed class SOAudio : ScriptableObject
{
    [SerializeField] private List<AudioClip> m_listClips = new();          // 같은 소리의 변형들 - 랜덤 재생으로 반복감 감소
    [SerializeField] private AudioMixerGroup m_refOutputGroup;
    [SerializeField, Range(0f, 1f)] private float m_fVolume = 1.0f;
    [SerializeField, Range(0.1f, 3f)] private float m_fPitchMin = 1.0f;    // 연속 재생 시 기계적인 느낌 완화
    [SerializeField, Range(0.1f, 3f)] private float m_fPitchMax = 1.0f;
    [SerializeField, Range(0f, 1f)] private float m_fSpatialBlend = 0.0f;  // 0 = 2D(플레이어 무기/UI), 1 = 3D(몬스터)
    [SerializeField] private float m_fMinInterval = 0.0f;                  // 같은 SO의 최소 재생 간격(초). 연사/피격음 폭주 방지
    [SerializeField, Range(0, 256)] private int m_iPriority = 128;         // 낮을수록 중요 (AudioSource.priority와 같은 의미)
    [SerializeField] private bool m_bLoop = false;

    public AudioMixerGroup OutputGroup => m_refOutputGroup;
    public float Volume => m_fVolume;
    public float SpatialBlend => m_fSpatialBlend;
    public float MinInterval => m_fMinInterval;
    public int Priority => m_iPriority;
    public bool Loop => m_bLoop;

    public AudioClip GetRandomClip()
    {
        if (m_listClips.Count == 0)
            return null;
        return m_listClips[Random.Range(0, m_listClips.Count)];
    }

    public float GetRandomPitch() => Random.Range(m_fPitchMin, m_fPitchMax);
}
