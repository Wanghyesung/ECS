using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                DungeonManager
기능 : 던전 한 판의 진행(스테이지 순서, 보스 등장, 스테이지 클리어)을 책임지는 매니저.
       List<SOStage>를 순서대로 진행하며, 각 스테이지 시작 시 ObjectSpawner에게
       그 스테이지의 몬스터 스폰 테이블을 예약시킨다.
       보스는 마지막 스테이지에서만 등장 — 그 전 스테이지들은 몬스터를 다 처치하면
       보스 없이 바로 다음 스테이지로 넘어간다.
 *///////////////////////////////////////////
public class DungeonManager : MonoBehaviour
{
    public static DungeonManager m_Instance = null;

    [SerializeField] private ObjectSpawner m_refSpawner;
    [SerializeField] private List<SOStage> m_listStage = new List<SOStage>();

    private int m_iCurrentStageIdx = 0;
    private int m_iRemainMonsterCount = 0;
    private bool m_bBossSpawned = false;

    private void Awake()
    {
        if (m_Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
    }

    private void OnDestroy()
    {
        if (m_Instance == this)
            m_Instance = null;
    }

    private void OnEnable()
    {
        Monster.OnMonsterDied += MonsterDead;
    }

    private void OnDisable()
    {
        Monster.OnMonsterDied -= MonsterDead;
    }

    private void Start()
    {
        if (m_refSpawner == null)
        {
            Debug.Log("Spawner 미설정 : DungeonManager");
            return;
        }

        StartStage(0);
    }

    private void StartStage(int _iStageIdx)
    {
        if (_iStageIdx >= m_listStage.Count)
        {
            Debug.Log("던전 클리어 : DungeonManager");
            return;
        }

        m_iCurrentStageIdx = _iStageIdx;
        m_bBossSpawned = false;

        SOStage refStage = m_listStage[_iStageIdx];
        m_iRemainMonsterCount = refStage.ListSpawnEntry.Count;

        for (int i = 0; i < refStage.ListSpawnEntry.Count; ++i)
        {
            var tEntry = refStage.ListSpawnEntry[i];
            m_refSpawner.AddSpawnObject(tEntry.fSpawnTime, tEntry.MonsterPrefab, tEntry.vPosition);
        }
    }

    private void MonsterDead(int _iExpReward)
    {
        if (m_bBossSpawned == true)
        {
            // 보스는 마지막 스테이지에서만 등장하므로, 보스가 죽었다는 건 곧 던전 클리어
            StartStage(m_iCurrentStageIdx + 1);
            return;
        }

        --m_iRemainMonsterCount;
        if (m_iRemainMonsterCount <= 0)
            SpawnBoss();
    }

    // 그 스테이지의 일반 몬스터를 다 처치했을 때 호출: 마지막 스테이지면 보스 등장, 아니면 다음 스테이지로
    
    private void SpawnBoss()
    {
        m_bBossSpawned = true;

        SOStage refStage = m_listStage[m_iCurrentStageIdx];
        m_refSpawner.AddSpawnObject(0.0f, refStage.BossPrefab, refStage.BossSpawnPosition);
    }
}
