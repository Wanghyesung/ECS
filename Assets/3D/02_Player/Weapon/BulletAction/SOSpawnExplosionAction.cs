using UnityEngine;

/*///////////////////////////////////////////
                SOSpawnExplosionAction
기능 : 총알 도착 위치에 폭발 이펙트(PoolObject)를 스폰
       기존 Missiles.SpawnExplosion 로직을 BulletArriveAction으로 분리한 것
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_SpawnExplosionAction", menuName = "Game/Weapon/BulletAction/SpawnExplosion")]
public class SOSpawnExplosionAction : SOBulletAction
{
    [SerializeField] private PoolObject m_refExplodeObj;

    public override void Execute(IAttackObject _refOwner)
    {
        if (m_refExplodeObj == null)
            return;

        GameObject refExObject = ObjectPool.m_Instance.GetObject(m_refExplodeObj);
        if (refExObject == null)
            return;

        refExObject.transform.position = _refOwner.transform.position;
    }
}
