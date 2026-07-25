using System;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                  SOStage
기능 : 던전 한 스테이지에서 등장할 몬스터 스폰 테이블과 보스 정보를 담는 데이터.
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_Stage", menuName = "Game/Dungeon/Stage")]
public class SOStage : ScriptableObject
{
    [Serializable]
    public struct tSpawnData
    {
        public float fSpawnTime; //스테이지 시작 후 몇 초 뒤에 스폰할지
        public PoolObject MonsterPrefab;
        public Vector3 vPosition;
    }

    public List<tSpawnData> ListSpawnEntry = new List<tSpawnData>();

    [Header("Boss")]
    public PoolObject BossPrefab;
    public Vector3 BossSpawnPosition;
}
