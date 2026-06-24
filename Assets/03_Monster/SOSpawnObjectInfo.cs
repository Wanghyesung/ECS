using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*///////////////////////////////////////////
               SOSpawnObjectInfo
기능 : 게임 오브젝트가 생성하는 스폰 오브젝트를 관리
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_SpawnObject", menuName = "Game/Spawn/SpawnObject")]

public class SOSpawnObjectInfo : ScriptableObject
{
    [Header("오브젝트 옵션")]
    public SOAttackInfo SOAttackInfo;
    public PoolObject AttackObject;

    [Header("스폰 옵션")]
    public int SpawnCount;
    public float SpawnTime;
}
