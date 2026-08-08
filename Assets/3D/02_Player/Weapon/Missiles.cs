using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/*///////////////////////////////////////////
                Missiles
기능 : 지정된 위치로 회전하면서 목적지로 이동 후 공격.
       위치/방향 계산과 Transform 적용까지 전부 MissileMoveManager의 Job(TransformAccessArray)이
       한다. 여기선 발사 시점에 필요한 정보를 매니저에 넘기고, Job이 못 하는 매니지드 오브젝트
       호출(넉백 방향 동기화/도착 시 풀 반납)만 콜백으로 받는다.
 *///////////////////////////////////////////

public class Missiles : Bullet
{
    [SerializeField] private bool m_bTraceTarget = false;

    protected override void Awake()
    {
        base.Awake();
    }

    protected override int RegisterMoveJob()
    {
        return MissileMoveManager.m_Instance.RegisterPermanent(this);
    }

    protected override void ActivateMoveJob()
    {
        Vector3 vTargetPos = m_tShotInfo.TargetPos;
        if (m_bTraceTarget == true && m_tShotInfo.TargetTr != null)
            vTargetPos = m_tShotInfo.TargetTr.position;

        float fTargetLength = Mathf.Max((vTargetPos - transform.position).magnitude, 0.01f);

        MissileMoveManager.m_Instance.Activate(
            m_iMoveManagerIndex, m_tShotInfo.Speed, fTargetLength,
            m_refAttackInfo.ProximityRadius, m_refAttackInfo.RotationSpeed,
            m_refAttackInfo.MaxRotationSpeed, m_refAttackInfo.RotateSpeedRate,
            m_bTraceTarget, m_tShotInfo.TargetTr, m_tShotInfo.TargetPos);
    }

    protected override void UnactivateMoveJob()
    {
        if (m_iMoveManagerIndex >= 0)
            MissileMoveManager.m_Instance.Deactivate(m_iMoveManagerIndex);
    }

    // MissileMoveManager가 Job 완료 후 매 스텝 최신 진행방향을 되돌려줌 (피격 시 넉백 방향 계산용)
    public void SyncMoveDir(Vector3 _vMoveDir)
    {
        m_tShotInfo.MoveDir = _vMoveDir;
    }

    // MissileMoveManager가 도착 판정을 받으면 호출. AliveTime을 0으로 만들어
    // PoolObject.Update()가 다음 틱에 자연스럽게 풀로 반납하게 함
    public void Arrived()
    {
        m_refPoolObj.SetAliveTime(0.0f);
    }

    protected override void AttackMonster(CircleCollider _refOther)
    {
        base.AttackMonster(_refOther);
    }

    public override void SetAttack(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        base.SetAttack(_refAttackInfo, _refShotInfo);
    }
}
