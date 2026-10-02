using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_MonsterInfo", menuName = "Game/MonsterInfo")]

public class SOMonsterInfo : SOObjectInfo
{
    [Header("Reward")]
    public int ExpReward = 10;

    [Header("Audio")]
    [SerializeField] private SOAudio m_SODeadAudio;

    [Header("Death Effect")]
    [SerializeField, Min(0.1f)] private float m_fDeadEffectScale = 1f;

    public SOAudio DeadAudio => m_SODeadAudio;
    public float DeadEffectScale => m_fDeadEffectScale > 0f ? m_fDeadEffectScale : 1f;
}
