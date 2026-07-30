using UnityEngine;

/*///////////////////////////////////////////
              JobGuidedBullet
목적 : GuidedBullet과 동일하게 "타겟을 향해 도는 총알". 위치/방향 계산은
       GuidedMoveManager의 Job(Burst)이 병렬로 하고, 실제 적용은 매 스텝
       Rigidbody.MoveRotation + MovePosition으로 한다.
 *///////////////////////////////////////////

public class JobGuidedBullet : Bullet
{
    private int m_iGuidedJobIndex = -1;

    protected override void Awake()
    {
        base.Awake();
        m_iGuidedJobIndex = GuidedMoveManager.m_Instance.RegisterPermanent(this);
    }

    protected override void FixedUpdate()
    {
        // 위치/회전 계산·적용 전부 GuidedMoveManager가 처리하므로 여기서는 아무것도 하지 않음
    }

    public override void SetAttack(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        base.SetAttack(_refAttackInfo, _refShotInfo);

        GuidedMoveManager.m_Instance.Activate(
            m_iGuidedJobIndex, transform.position, transform.forward,
            _refShotInfo.Speed, _refShotInfo.TargetTr);
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (m_iGuidedJobIndex >= 0)
            GuidedMoveManager.m_Instance.Deactivate(m_iGuidedJobIndex);
    }

    protected override void AttackMonster(Collider other)
    {
        base.AttackMonster(other);
    }

    // GuidedMoveManager가 Job 완료 후 호출. 원본 GuidedBullet과 동일하게 Rigidbody API로 적용
    public void ApplyMove(Vector3 _vNewPos, Quaternion _qNewRot)
    {
        m_refRigidbody.MoveRotation(_qNewRot);
        m_refRigidbody.MovePosition(_vNewPos);
    }
}
