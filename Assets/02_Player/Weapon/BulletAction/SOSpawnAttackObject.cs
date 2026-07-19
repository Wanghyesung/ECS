using UnityEngine;

/*///////////////////////////////////////////
                SOSpawnAttackObject
기능 : 총알 도착 위치에 AttackObject(정지형 범위 공격 오브젝트, 예: 폭발)를 스폰.
       owner의 AttackInfo를 그대로 넘겨 SetAttack을 호출하면,
       AttackObject.SetAttack이 Damage만 뽑아 캐싱된 자기 AttackInfo에 반영한다.
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_SpawnAttackObject", menuName = "Game/Weapon/BulletAction/SpawnAttackObject")]
public class SOSpawnAttackObject : SOBulletAction
{
    [SerializeField] private AttackObject m_refAttackObjectPrefab;

    // 총알이 도착할 때마다 매번 터지면 과해서, 이 확률을 통과했을 때만 스폰
    [Range(0f, 1f)]
    [SerializeField] private float m_fTriggerChance = 0.3f;

    [Header("Scale By Level")]
    [SerializeField] private float m_fBaseRadius = 1f;
    [SerializeField] private float m_fRadiusPerLevel = 0.1f;

    public override void Execute(IAttackObject _refOwner)
    {
        if (m_refAttackObjectPrefab == null)
            return;

        if (Random.value > m_fTriggerChance)
            return;

        PoolObject refPrefabPoolObj = m_refAttackObjectPrefab.GetComponent<PoolObject>();
        GameObject refObj = ObjectPool.m_Instance.GetObject(refPrefabPoolObj);
        if (refObj == null)
            return;

        refObj.transform.position = _refOwner.transform.position;

        AttackObject refAttackObj = refObj.GetComponent<AttackObject>();
        refAttackObj.SetAttack(_refOwner.AttackInfo, new tShotInfo());

        //refAttackObj.SetWeaponHitActions(_refOwner.)
        int iLevel = BattleManager.m_Instance.CurrentLevel;
        refAttackObj.SetScale(m_fBaseRadius + m_fRadiusPerLevel * iLevel);
    }
}
