using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_Attack_Info", menuName = "Game/Attack Info")]

public class AttackInfo : ScriptableObject
{
    [TextArea]
    public string Description;

    [Header("Stats")]
    public int Damage = 10;
    public int AttackPower = 0;
    public float Range = 1.5f;
    public float HitboxRadius = 0.5f;
    public float Cooldown = 0.5f;
    public int HitCount = 1;

    [Header("Knockback / Stun")]
    public float KnockbackForce = 3f;
    public float KnockbackDuration = 0.2f;
    public float StunDuration = 0f;

    [Header("Critical / Misc")]
    [Range(0f, 1f)]
    public float CriticalChance = 0f;

    [Header("Targeting")]
    public LayerMask HitLayers = ~0;

    [Header("Projectile")]
    public bool IsProjectile = false;
    public GameObject ProjectilePrefab;
    public float ProjectileSpeed = 10f;

    [Header("Visuals / Audio")]
    public ParticleSystem HitEffect;
    public AudioClip HitSound;
    public string AnimationTrigger;

}
