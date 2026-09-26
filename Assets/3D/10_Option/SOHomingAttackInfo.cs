using System;
using UnityEngine;

/*///////////////////////////////////////////
            SOHomingAttackInfo
목적 : 유도탄(Missiles/JobMissile) 전용 데이터. 탄 데이터에 회전 유도값 4개를 얹는다.
       Bullet/GuidedBullet 은 이 값을 쓰지 않으므로 SOAttackInfo 그대로 사용
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_HomingAttack_Info", menuName = "Game/Homing Attack Info")]
public class SOHomingAttackInfo : SOAttackInfo
{
    [Header("Homing")]
    public float BaseRotationSpeed = 90f;
    public float MaxRotationSpeed = 180f;
    public float RotationAccelRate = 0f;
    public float ProximityRadius = 1.5f;

    public override AttackInfo MakeAttackInfo()
    {
        HomingAttackInfo refHomingInfo = new HomingAttackInfo();

        refHomingInfo.AttackPower = AttackPower;
        refHomingInfo.Damage = Damage;
        refHomingInfo.MaxHitCount = HitCount;
        refHomingInfo.HitStep = HitStep;
        refHomingInfo.LineDuration = TelegraphDuration;

        refHomingInfo.AliveTime = AliveTime;
        refHomingInfo.Speed = Speed;

        refHomingInfo.KnockbackForce = KnockbackForce;
        refHomingInfo.KnockbackDuration = KnockbackDuration;

        refHomingInfo.HitLayers = HitLayers;
        refHomingInfo.HitAudio = HitAudio;

        refHomingInfo.RotationSpeed = BaseRotationSpeed;
        refHomingInfo.MaxRotationSpeed = MaxRotationSpeed;
        refHomingInfo.RotateSpeedRate = RotationAccelRate;
        refHomingInfo.ProximityRadius = ProximityRadius;
        return refHomingInfo;
    }
}

/*///////////////////////////////////////////
              HomingAttackInfo
목적 : SOHomingAttackInfo 가 만드는 런타임 데이터. Missiles/JobMissile 이 SetAttack 에서
       as 로 받아 캐싱해 두고 MissileMoveManager.Activate 에 넘긴다
 *///////////////////////////////////////////

[Serializable]
public class HomingAttackInfo : AttackInfo
{
    [Header("Homing")]
    public float RotationSpeed = 90f;
    public float MaxRotationSpeed = 180f;
    public float RotateSpeedRate = 0f;
    public float ProximityRadius = 1.5f;
}
