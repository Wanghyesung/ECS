using UnityEngine;

/*///////////////////////////////////////////
                SOSpawnRadialDirAction
기능 : 총알 도착 위치에 정된 위치와 방향으로 공격 오브젝트 발사
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_SpawnRadialDirAction", menuName = "Game/Weapon/BulletArriveAction/SOSpawnRadialDirAction")]
public class SOSpawnRadialDirAction : SOBulletArriveAction
{
    [SerializeField] private PoolObject m_refExplodeObj;
    
    public override void Execute(Bullet _refOwner)
    {
        if (m_refExplodeObj == null)
            return;

        GameObject refExObject = ObjectPool.m_Instance.GetObject(m_refExplodeObj);
        if (refExObject == null)
            return;

        refExObject.transform.position = _refOwner.transform.position;
    }
}
