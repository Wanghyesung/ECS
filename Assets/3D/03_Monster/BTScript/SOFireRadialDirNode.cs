using TMPro;
using UnityEngine;


/*///////////////////////////////////////////
           SOFireRadialDirNode
기능 : 지정된 위치와 방향으로 공격 오브젝트 발사
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO)FireRadialDirNode", menuName = "Game/Monster/ActionNode/FireRadialDirNode")]

public class SOFireRadialDirNode : SONode
{
    private readonly float GOLDEN_RATIO = ((1f + Mathf.Sqrt(5f)) / 2f);
    public override eNodeState Execute(BlackBoard _refBB)
    {

        SpawnInfo refSpawnInfo = _refBB.CurrentAttackSpawn;
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