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
    [SerializeField] private SOPoolData m_refAttackObjectPoolData;

    // 총알이 도착할 때마다 매번 터지면 과해서, 이 확률을 통과했을 때만 스폰
    [Range(0f, 1f)]
    [SerializeField] private float m_fTriggerChance = 0.3f;

    [Header("Scale By Level")]
    [SerializeField] private float m_fBaseRadius = 1f;
    [SerializeField] private float m_fRadiusPerLevel = 0.1f;

    [Header("Hit Count")]
    // 폭발 하나가 데미지를 줄 수 있는 최대 몬스터 수.
    // CircleCollider의 Enter 이벤트는 몬스터당 1회만 오므로 "명중 횟수 = 맞은 몬스터 수"
    [Min(1)]
    [SerializeField] private int m_iMaxHitCount = 5;

    public override void Execute(IAttackObject _refOwner)
    {
        if (m_refAttackObjectPoolData == null)
            return;

        if (Random.value > m_fTriggerChance)
            return;

        GameObject refObj = ObjectPoolManager.m_Instance.GetObject(m_refAttackObjectPoolData);
        if (refObj == null)
            return;

        refObj.transform.position = _refOwner.transform.position;

        AttackObject refAttackObj = refObj.GetComponent<AttackObject>();
        refAttackObj.SetAttack(_refOwner.AttackInfo, new tShotInfo());
        refAttackObj.SetMaxHitCount(m_iMaxHitCount);

        //refAttackObj.SetWeaponHitActions(_refOwner.)
        int iLevel = BattleManager.m_Instance.Level.CurrentValue;
        refAttackObj.SetScale(m_fBaseRadius + m_fRadiusPerLevel * iLevel);

//#if UNITY_EDITOR
//        UnityEditor.EditorApplication.isPaused = true; // ◄ 에디터 일시정지
//#endif
    }
}
