using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_MonsterInfo", menuName = "Game/Monster Info")]
public class SOMonsterInfo : ScriptableObject
{
    [Header("Identity")]
    public string MonsterName = "New Monster";

    [TextArea]
    public string Description;
    
    [Header("Stats")]
    public int MaxHP = 10;
    public float Speed = 3f;
    public float AttackRange = 1.5f;


    [Header("Attack")]
    public SOAttackInfo AttackInfo = null;
}

public enum eEntityState
{
    None,
    Idle,
    Move,
    Attack,
    Hit,
    Dead,
    End,
}

public class MonsterInfo
{
    public int HP;
    public float Speed;

    public eEntityState State;
}
