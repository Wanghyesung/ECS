using System;
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

    [Header("Critical / Misc")]
    [Range(0f, 1f)]
    public float CriticalChance = 0f;

    [Header("Targeting")]
    public LayerMask HitLayers = ~0;

    [Header("Audio")]
    public AudioClip HitSound;

    public AttackInfo MakeAttackInfo()
    {
        AttackInfo refAttackInfo = new AttackInfo();

        refAttackInfo.AttackPower = AttackPower;
        refAttackInfo.AttackSpeed = Speed;
        refAttackInfo.Damage = Damage;
        refAttackInfo.AliveTime = AliveTime;
        refAttackInfo.CoolDown = Cooldown;

        refAttackInfo.RotationSpeed = BaseRotationSpeed;
       
        refAttackInfo.HitLayers = HitLayers;
        return refAttackInfo;
    }
}



/*///////////////////////////////////////////
                AttackInfo
기능 : SOAttackInfo 정보에서 동적으로 데이터 변경하는 데이터
 *///////////////////////////////////////////

[Serializable]
public class AttackInfo
{
    [Header("Dynamic Info")]
    public int Damage;
    public int AttackPower;

    public float AliveTime;
    public float AttackSpeed;
    public float CoolDown;


    [Header("Homing")]
    public float RotationSpeed = 90f;

    [Header("Target")]
    public Vector3 TargetPos;
    public Transform TargetTrasnform = null;
    public Vector3 HitPosition;


    [Header("Tem")]
    public LayerMask HitLayers = ~0;
}
