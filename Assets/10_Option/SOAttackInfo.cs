using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using static PoolObject;
using static Weapon;

[CreateAssetMenu(fileName = "SO_Attack_Info", menuName = "Game/Attack Info")]

public class SOAttackInfo : ScriptableObject
{
    [TextArea]
    public string Description;

    [Header("Stats")]
    public eWeaponType WeaponType;
    public ePoolType PoolType;
    public int Damage = 10;
    public int AttackPower = 0;
    public float Cooldown = 0.5f;
    public int HitCount = 1;
    public float Speed = 12.0f;
    public float AliveTime = 0.2f;

    [Header("Knockback / Stun")]
    public float KnockbackForce = 3f;
    public float KnockbackDuration = 0.2f;
    public float StunDuration = 0f;

    [Header("Homing")]
    public float BaseRotationSpeed = 90f;
    public float HomingRaius = 1000.0f;

    [Header("Critical / Misc")]
    [Range(0f, 1f)]
    public float CriticalChance = 0f;

    [Header("Targeting")]
    public LayerMask HitLayers = ~0;

    [Header("Audio")]
    public AudioClip HitSound;
}


//동적 영역 
public struct tAttackInfo
{
    public int Damage;
    public int AttackPower;
    public float KnockbackForce;
    public float KnockbackDuration;
    public float StunDuration;

    public Vector3 HitPosition;
}
