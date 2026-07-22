using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_MonsterInfo", menuName = "Game/MonsterInfo")]

public class SOMonsterInfo : SOObjectInfo
{
    [Header("Reward")]
    public int ExpReward = 10;
}
