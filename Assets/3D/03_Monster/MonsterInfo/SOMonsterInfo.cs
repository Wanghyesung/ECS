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

    public SOAudio DeadAudio => m_SODeadAudio;
}
