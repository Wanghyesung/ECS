using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*///////////////////////////////////////////
              GuidedBullet
기능 : 타겟을 향해가는 총알.
       위치/방향 계산과 Transform 적용까지 전부 GuidedMoveManager의 Job(TransformAccessArray)이
       한다. 여기선 발사 시점에 필요한 정보(속도, 타겟)를 매니저에 넘기기만 하면 됨.
 *///////////////////////////////////////////

public class GuidedBullet : Bullet
{
    protected override void Awake()
    {
        base.Awake();
    }

    protected override int RegisterMoveJob()
    {
        return GuidedMoveManager.m_Instance.RegisterPermanent(this);
    }

    protected override void ActivateMoveJob()
    {
        GuidedMoveManager.m_Instance.Activate(m_iMoveManagerIndex, m_tShotInfo.Speed, m_tShotInfo.TargetTr);
    }

    protected override void UnactivateMoveJob()
    {
        if (m_iMoveManagerIndex >= 0)
            GuidedMoveManager.m_Instance.Deactivate(m_iMoveManagerIndex);
    }

    public override void SetAttack(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        base.SetAttack(_refAttackInfo, _refShotInfo);
    }

    protected override void AttackMonster(CircleCollider _refOther)
    {
        base.AttackMonster(_refOther);
    }
}
