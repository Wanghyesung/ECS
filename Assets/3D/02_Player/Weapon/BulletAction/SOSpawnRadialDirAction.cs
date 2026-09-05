using UnityEngine;

/*///////////////////////////////////////////
                SOSpawnRadialDirAction
기능 : 총알 도착 위치에서 사방(구면)으로 새 공격 오브젝트를 스폰
 *///////////////////////////////////////////


[CreateAssetMenu(fileName = "SO_SpawnRadialDirAction", menuName = "Game/Weapon/BulletAction/SOSpawnRadialDirAction")]
public class SOSpawnRadialDirAction : SOBulletAction
{
    [SerializeField] private SOPoolData m_refSpawnBulletPrefab;
    [SerializeField] private int m_iSpawnCount = 8;
    [SerializeField] private float m_fSpeed = 10f;

    private static readonly float GOLDEN_RATIO = (1f + Mathf.Sqrt(5f)) / 2f;

    public override void Execute(IAttackObject _refOwner)
    {
        if (m_refSpawnBulletPrefab == null || m_iSpawnCount <= 0)
            return;

        for (int i = 0; i < m_iSpawnCount; ++i)
        {
            float fY = 1f - (i / (float)Mathf.Max(m_iSpawnCount - 1, 1)) * 2f;
            float fRadiusAtY = Mathf.Sqrt(Mathf.Max(0f, 1f - fY * fY));
            float fTheta = 2f * Mathf.PI * GOLDEN_RATIO * i;

            Vector3 vDir = new Vector3(Mathf.Cos(fTheta) * fRadiusAtY, fY, Mathf.Sin(fTheta) * fRadiusAtY);

            tShotInfo refShotInfo = new tShotInfo();
            refShotInfo.Speed = m_fSpeed;

            Bullet.SpawnAttackObject(m_refSpawnBulletPrefab, _refOwner.transform.position, Quaternion.LookRotation(vDir), _refOwner.AttackInfo, refShotInfo);
        }
    }
}
