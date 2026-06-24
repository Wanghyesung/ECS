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
    [Header("Origin Prefab")]
    public PoolObject PoolObject;

    [Header("Object Attack Info")]
    public SOAttackInfo AttackInfo;

    [Header("Spawn Option")]
    public int SpawnCount;
    public float SpawnTime;
}
