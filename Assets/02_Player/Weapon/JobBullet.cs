using UnityEngine;

/*///////////////////////////////////////////
              JobBullet
목적 : Bullet과 동일한 직선 이동 총알. 위치 계산은 BulletMoveManager의 Job(Burst)이 병렬로 하고,
       실제 적용은 매 스텝 Rigidbody.MovePosition으로 한다 (Transform 직접 쓰기로 인한
       Physics.SyncColliderTransform 비용을 피하기 위함).
 *///////////////////////////////////////////

public class JobBullet : Bullet
{
    private int m_iJobIndex = -1;

    protected override void Awake()
    {
        base.Awake();
        m_iJobIndex = BulletMoveManager.m_Instance.RegisterPermanent(this);
    }

    public override void SetAttack(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        base.SetAttack(_refAttackInfo, _refShotInfo);

        //BulletMoveManager.m_Instance.Activate(m_iJobIndex, transform.position, transform.forward, _refShotInfo.Speed);
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (m_iJobIndex >= 0)
            BulletMoveManager.m_Instance.Deactivate(m_iJobIndex);
    }

    // BulletMoveManager가 Job 완료 후 호출. 원본 Bullet.FixedUpdate()와 동일하게 Rigidbody API로 적용
    public void ApplyMove(Vector3 _vNewPos)
    {
        m_refRigidbody.MovePosition(_vNewPos);
    }
}
