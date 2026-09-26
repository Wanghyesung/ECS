using UnityEngine;

/*///////////////////////////////////////////
                SOSpawnExplosionAction
기능 : 총알 도착 위치에 폭발 이펙트(PoolObject)를 스폰
       기존 Missiles.SpawnExplosion 로직을 BulletArriveAction으로 분리한 것
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_SpawnExplosionAction", menuName = "Game/Weapon/BulletAction/SpawnExplosion")]
public class SOSpawnExplosionAction : SOBulletAction
{
    [SerializeField] private SOPoolData m_refExplodeObj;
    [SerializeField] private SOAudio m_SOExplodeAudio;

    public override void Execute(IAttackObject _refOwner)
    {
        Vector3 vPos = _refOwner.transform.position;

        // 이펙트 풀이 비어도 소리는 난다
        SoundManager.m_Instance.PlaySfx(m_SOExplodeAudio, vPos);

        if (m_refExplodeObj == null)
            return;

        GameObject refExObject = ObjectPoolManager.m_Instance.GetObject(m_refExplodeObj);
        if (refExObject == null)
            return;

        refExObject.transform.position = vPos;
    }
}
