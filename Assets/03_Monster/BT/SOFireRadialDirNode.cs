using TMPro;
using UnityEngine;


/*///////////////////////////////////////////
           SOFireRadialDirNode
��� : 360�� ������ ������ ������Ʈ ���� ����
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
            // �ε����� ������� -1~1 ������ Y��(����) ���
            float y = 1f - (i / (float)iTotalCount) * 2f;

            // �ش� ���̿����� ������ ���
            float fRadiusAtY = Mathf.Sqrt(1f - y * y);

            // Ȳ�ݺ� �̿��� ����(theta) ���
            float fTheta = 2f * Mathf.PI * GOLDEN_RATIO * i;

            // �� ǥ���� X, Z ��ǥ ���
            float x = Mathf.Cos(fTheta) * fRadiusAtY;
            float z = Mathf.Sin(fTheta) * fRadiusAtY;

            // ���� ���� ���� (normalized ����)
            Vector3 vDir = new Vector3(x, y, z);

            refAttackWeapon.FireAndRotate(vDir, refSpawnInfo.SpawnFowardOffset);  
        }

        return eNodeState.Success;
    }
}
