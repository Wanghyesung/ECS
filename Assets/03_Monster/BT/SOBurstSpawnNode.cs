using UnityEngine;


/*///////////////////////////////////////////
            SOBurstSpawnNode
기능 : 일정 시간 동안 GuidedBullet을 주기적으로 스폰
       - IsBursting이 false면 버스트 시작
       - 버스트 중에는 Interval마다 총알 스폰 후 Running 반환
       - Duration이 지나면 IsBursting 해제 후 Success 반환
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_BurstSpawnNode", menuName = "Game/Monster/ActionNode/BurstSpawnNode")]
public class SOBurstSpawnNode : SONode
{
    [Tooltip("ListSpawnObject에서 사용할 SpawnInfo 인덱스")]
    [SerializeField] private int m_iSpawnInfoIdx = 0;

    [Tooltip("총알을 발사할 총 지속 시간 (초)")]
    [SerializeField] private float m_fDuration = 4f;

    [Tooltip("총알 스폰 간격 (초)")]
    [SerializeField] private float m_fInterval = 0.3f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        float fNow = Time.time;

        if (_refBB.IsBursting)
            return UpdateBurst(_refBB, fNow);

        return StartBurst(_refBB, fNow);
    }

    private eNodeState StartBurst(BlackBoard _refBB, float fNow)
    {
        _refBB.IsBursting = true;
        _refBB.BurstEndTime = fNow + m_fDuration;

        SpawnGuidedBullet(_refBB);
        _refBB.BurstNextSpawnTime = fNow + m_fInterval;

        return eNodeState.Running;
    }

    private eNodeState UpdateBurst(BlackBoard _refBB, float fNow)
    {
        if (fNow >= _refBB.BurstEndTime)
        {
            _refBB.IsBursting = false;
            return eNodeState.Success;
        }

        if (fNow >= _refBB.BurstNextSpawnTime)
        {
            SpawnGuidedBullet(_refBB);
            _refBB.BurstNextSpawnTime = fNow + m_fInterval;
        }

        return eNodeState.Running;
    }

    private void SpawnGuidedBullet(BlackBoard _refBB)
    {
        if (_refBB.Owner == null || _refBB.TargetTr == null)
            return;

        var listSpawn = _refBB.Owner.ListSpawnObject;
        if (listSpawn == null || m_iSpawnInfoIdx >= listSpawn.Count)
            return;

        SpawnInfo spawnInfo = listSpawn[m_iSpawnInfoIdx];
        SOSpawnObjectInfo spawnObjInfo = spawnInfo.SpawnObjectInfo;
        if (spawnObjInfo == null || spawnObjInfo.AttackInfo == null)
            return;

        int iPoolCount = ObjectPool.m_Instance.GetObjectCount(spawnObjInfo.AttackInfo.PoolType);
        int iSpawnCount = spawnObjInfo.SpawnCount;
        if (iPoolCount < iSpawnCount)
            return;

        Vector3 vSpawnPos = spawnInfo.SpawnTransform != null
            ? spawnInfo.SpawnTransform.position
            : _refBB.Owner.transform.position;

        for (int i = 0; i < iSpawnCount; ++i)
        {
            GameObject refObj = ObjectPool.m_Instance.GetObject(spawnObjInfo.AttackInfo.PoolType);
            if (refObj == null)
                continue;

            refObj.transform.position = vSpawnPos;

            if (refObj.TryGetComponent<GuidedBullet>(out var guidedBullet))
            {
                guidedBullet.SetTarget(_refBB.TargetTr);
                guidedBullet.SetAttack(spawnObjInfo.AttackInfo);
            }
        }
    }
}
