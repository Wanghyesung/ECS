using UnityEngine;

/*///////////////////////////////////////////
              JobMissile
목적 : Missiles와 동일하게 "가속 회전으로 유도되다 목표 근처에서 도착 처리되는 총알".
       위치/방향 계산은 MissileMoveManager의 Job(Burst)이 병렬로 하고, 실제 적용은
       매 스텝 Rigidbody.MoveRotation + MovePosition으로 한다.
 *///////////////////////////////////////////

public class JobMissile : Bullet
{
    [SerializeField] private bool m_bTraceTarget = false;

    private int m_iJobIndex = -1;

    protected override void Awake()
    {
        base.Awake();
        //m_iJobIndex = MissileMoveManager.m_Instance.RegisterPermanent(this);
    }


    public override void SetAttack(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        base.SetAttack(_refAttackInfo, _refShotInfo);

        Vector3 vTargetPos = _refShotInfo.TargetPos;
        if (m_bTraceTarget == true && _refShotInfo.TargetTr != null)
            vTargetPos = _refShotInfo.TargetTr.position;

        float fTargetLength = Mathf.Max((vTargetPos - transform.position).magnitude, 0.01f);

        MissileMoveManager.m_Instance.Activate(
            m_iJobIndex,  _refShotInfo.Speed, fTargetLength,
            _refAttackInfo.ProximityRadius, _refAttackInfo.RotationSpeed,
            _refAttackInfo.MaxRotationSpeed, _refAttackInfo.RotateSpeedRate,
            m_bTraceTarget, _refShotInfo.TargetTr, _refShotInfo.TargetPos);
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (m_iJobIndex >= 0)
            MissileMoveManager.m_Instance.Deactivate(m_iJobIndex);
    }

    // MissileMoveManager가 Job 완료 후 매 스텝 최신 진행방향을 되돌려줌 (피격 시 넉백 방향 계산용)
    public void SyncMoveDir(Vector3 _vMoveDir)
    {
        m_tShotInfo.MoveDir = _vMoveDir;
    }

    // MissileMoveManager가 도착 판정을 받으면 호출. 기존 Missiles와 동일하게 AliveTime을 0으로 만들어
    // PoolObject.Update()가 다음 틱에 자연스럽게 풀로 반납하게 함
    public void MarkArrived()
    {
        m_refPoolObj.SetAliveTime(0.0f);
    }

    // MissileMoveManager가 Job 완료 후 호출. 원본 Missiles와 동일하게 Rigidbody API로 적용
    public void ApplyMove(Vector3 _vNewPos, Quaternion _qNewRot)
    {
        m_refRigidbody.MoveRotation(_qNewRot);
        m_refRigidbody.MovePosition(_vNewPos);
    }
}
