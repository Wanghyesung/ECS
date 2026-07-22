using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/*///////////////////////////////////////////
                ChargeNode
기능 : 몬스터 차지 오브젝트 실행 및 차지 시간 설정
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_ChargeDirFireNode", menuName = "Game/Monster/ActionNode/ChargeDirFireNode")]

public class SOChargeFireDirNode : SONode
{
    private readonly float GOLDEN_RATIO = ((1f + Mathf.Sqrt(5f)) / 2f);
    public override eNodeState Execute(BlackBoard _refBB)
    {
        Charge refCurCharge = _refBB.CurrentCharge;
        if (refCurCharge == null || _refBB.TargetTr == null)
            return eNodeState.Failure;

        if (refCurCharge.SpawnInfo == null)
            return eNodeState.Failure;

        SpawnInfo refSpawnInfo = _refBB.CurrentCharge.SpawnInfo;
        if (refSpawnInfo == null)
            return eNodeState.Failure;

        Weapon refAttackWeapon = refSpawnInfo.Weapon;
        int iPoolCount = ObjectPool.m_Instance.GetObjectCount(refAttackWeapon.FireBulletPrefab);
        int iTotalCount = refSpawnInfo.SpawnCount;

        if (iPoolCount < iTotalCount)
            return eNodeState.Failure;

        for (int i = 0; i < iTotalCount; ++i)
        {
            float y = 1f - (i / (float)iTotalCount) * 2f;

            float fRadiusAtY = Mathf.Sqrt(1f - y * y);

            float fTheta = 2f * Mathf.PI * GOLDEN_RATIO * i;

            float x = Mathf.Cos(fTheta) * fRadiusAtY;
            float z = Mathf.Sin(fTheta) * fRadiusAtY;

            Vector3 vDir = new Vector3(x, y, z);

            refAttackWeapon.FireAndRotate(vDir, refSpawnInfo.SpawnFowardOffset);
        }

        return eNodeState.Success;

    }
}

