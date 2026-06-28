using TMPro;
using UnityEngine;


/*///////////////////////////////////////////
           SetRadialDirectionNode
기능 : 360도 범위로 생성된 오브젝트 방향 설정
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_SetRadialDirNode", menuName = "Game/Monster/ActionNode/SetRadialDirNode")]

public class SOSetRadialDirNode : SONode
{
    private readonly float GOLDEN_RATIO = ((1f + Mathf.Sqrt(5f)) / 2f);
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if(_refBB.TargetTr == null)
            return eNodeState.Failure;

        SpawnInfo refSpawnInfo = _refBB.CurrentAttackSpawn;
        if (refSpawnInfo == null)
            return eNodeState.Failure;

        Weapon refAttackWeapon = refSpawnInfo.Weapon;
        int iPoolCount = ObjectPool.m_Instance.GetObjectCount(refAttackWeapon.FireBulletType);
        int iTotalCount = refSpawnInfo.SpawnCount;


        if (iPoolCount < iTotalCount)
            return eNodeState.Failure;

        for (int i = 0; i < iTotalCount; ++i)
        {
            // 인덱스를 기반으로 -1~1 사이의 Y값(높이) 계산
            float y = 1f - (i / (float)iTotalCount) * 2f;

            // 해당 높이에서의 반지름 계산
            float fRadiusAtY = Mathf.Sqrt(1f - y * y);

            // 황금비를 이용한 각도(theta) 계산
            float fTheta = 2f * Mathf.PI * GOLDEN_RATIO * i;

            // 구 표면의 X, Z 좌표 계산
            float x = Mathf.Cos(fTheta) * fRadiusAtY;
            float z = Mathf.Sin(fTheta) * fRadiusAtY;

            // 최종 방향 벡터 (normalized 상태)
            Vector3 vDir = new Vector3(x, y, z);

            refAttackWeapon.Fire(_refBB.TargetTr.position, _refBB.TargetTr, vDir, refSpawnInfo.SpawnFowardOffset);  
        }

        return eNodeState.Success;
    }
}
