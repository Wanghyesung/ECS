using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using static Weapon;

[CreateAssetMenu(fileName = "SO_Attack_Info", menuName = "Game/Attack Info")]

public class SOAttackInfo : ScriptableObject
{
    [TextArea]
    public string Description;

    [Header("Stats")]
    public eWeaponType WeaponType;
    public PoolObject PoolPrefab;

    public int Damage = 10;
    public int AttackPower = 0;
    public float Cooldown = 0.5f;
    public float Speed = 12.0f;
    public float SpeedOffset = 4.0f;
    public float AliveTime = 0.2f;

    [Header("Hit Count")]
    public int HitCount = 1;
    public float HitStep = 1.0f;


    [Header("Knockback / Stun")]
    public float KnockbackForce = 3f;
    public float KnockbackDuration = 0.2f;
    public float StunDuration = 0f;

   
    [Header("Homing")]
    public float BaseRotationSpeed = 90f;
    public float MaxRotationSpeed = 180f;
    public float RotationAccelRate = 0f;
    public float ProximityRadius = 1.5f;


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
        refAttackInfo.Damage = Damage;
        refAttackInfo.MaxHitCount = HitCount;
        refAttackInfo.HitStep = HitStep;

        refAttackInfo.AliveTime = AliveTime;
        refAttackInfo.CoolDown = Cooldown;
        refAttackInfo.Speed = Speed;

        refAttackInfo.RotationSpeed = BaseRotationSpeed;
        refAttackInfo.MaxRotationSpeed = MaxRotationSpeed;
        refAttackInfo.RotateSpeedRate = RotationAccelRate;
        refAttackInfo.ProximityRadius = ProximityRadius;

        refAttackInfo.KnockbackForce = KnockbackForce;
        refAttackInfo.KnockbackDuration = KnockbackDuration;

        refAttackInfo.HitLayers = HitLayers;
        return refAttackInfo;
    }
}



/*///////////////////////////////////////////
                AttackInfo
기능 : SO 적정 데이터를 기반으로 동적 데이터를 관리
 *///////////////////////////////////////////

[Serializable]
public class AttackInfo
{
    [Header("Owner")]
    public Transform Owner = null; //해당 오너는 weapon 오브젝트

    [Header("Dynamic Info")]
    public int Damage;
    public int AttackPower;
    public float AliveTime;
    public float CoolDown;
    public float Speed;

    [Header("Hit Count")]
    public int MaxHitCount;
    public float HitStep;

    [Header("Homing")]
    public float RotationSpeed = 90f;
    public float MaxRotationSpeed = 180f;
    public float RotateSpeedRate = 0f;
    public float ProximityRadius = 1.5f;
    

    [Header("Knockback")]
    public float KnockbackForce;
    public float KnockbackDuration;

    [Header("Tem")]
    public LayerMask HitLayers = ~0;
}

/*///////////////////////////////////////////
                tShotInfo
기능 : 발사(총알) 한 발마다 새로 생기는 동적 데이터.
       AttackInfo와 달리 struct라 SetAttack 호출 시 값으로 복사되어
       총알 인스턴스끼리 서로 값을 공유하지 않음
 *///////////////////////////////////////////

[Serializable]
public struct tShotInfo
{
    public Vector3 TargetPos;
    public Transform TargetTr;

    public Vector3 MoveDir;
    public Vector3 HitPosition;
    public float Speed;

    public int HitCount;
    public float LastHitTime;
}
