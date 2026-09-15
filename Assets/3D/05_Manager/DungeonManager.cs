using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    // 스포너가 꺼낸 몬스터 목록(보스 포함). 스폰 담당인 이쪽이 들고 있어야 Monster 클래스가 자기 개체수를
    // 몰라도 된다. 죽은(풀 반납=비활성) 몬스터는 CollectAliveMonsters에서 걸러내며 지연 제거
    private List<Monster> m_listSpawnedMonster = new List<Monster>();

    private void Awake()
    {
        if (m_Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (m_Instance == this)
            m_Instance = null;
    }

    private DisposableBag m_bagEvents;

    private void OnEnable()
    {
        Monster.OnMonsterDied.Subscribe(MonsterDead).AddTo(ref m_bagEvents);
        m_refSpawner.OnSpawned.Subscribe(HandleSpawned).AddTo(ref m_bagEvents);
    }

    private void OnDisable()
    {
        m_bagEvents.Clear();
    }

    public void StartStage(int _iStageIdx)
    {
        if (_iStageIdx >= m_listStage.Count)
        {
            Debug.Log("던전 클리어 : DungeonManager");
            GameSceneManager.m_Instance.LoadFirstScene();
            return;
        }

        m_iCurrentStageIdx = _iStageIdx;
        m_bBossSpawned = false;

        SOStage refStage = m_listStage[_iStageIdx];
        m_iRemainMonsterCount = refStage.ListSpawnEntry.Count;

        m_listSpawnedMonster.Clear();
        for (int i = 0; i < refStage.ListSpawnEntry.Count; ++i)
        {
            var tEntry = refStage.ListSpawnEntry[i];
            m_refSpawner.AddSpawnObject(tEntry.fSpawnTime, tEntry.MonsterPrefab, tEntry.vPosition);
        }
    }

    // 풀 재사용으로 같은 인스턴스가 다시 스폰될 수 있어 Contains로 중복 방지 (스폰 빈도가 낮아 O(n) 허용)
    private void HandleSpawned(GameObject _refSpawned)
    {
        if (_refSpawned.TryGetComponent(out Monster refMonster) == false)
            return;

        if (m_listSpawnedMonster.Contains(refMonster) == false)
            m_listSpawnedMonster.Add(refMonster);
    }

    // 핵폭탄 등 광역 카드가 호출: 살아있는(활성) 몬스터를 _listBuffer에 스냅샷으로 담고, 죽어서 풀에 반납된
    // 항목은 이때 목록에서 제거한다. 호출부가 TakeDamage로 죽여도 스냅샷을 순회하므로 안전
    public void CollectAliveMonsters(List<Monster> _listBuffer)
    {
        for (int i = m_listSpawnedMonster.Count - 1; i >= 0; --i)
        {
            Monster refMonster = m_listSpawnedMonster[i];
            if (refMonster.gameObject.activeInHierarchy)
            {
                _listBuffer.Add(refMonster);
                continue;
            }

            // 순서 무관하니 마지막 원소와 교체 후 제거 (FeatureManager.RequestFeature와 같은 O(1) 제거)
            int iLast = m_listSpawnedMonster.Count - 1;
            m_listSpawnedMonster[i] = m_listSpawnedMonster[iLast];
            m_listSpawnedMonster.RemoveAt(iLast);
        }
    }

    private void MonsterDead(int _iExpReward)
    {
        if (m_bBossSpawned == true)
        {
            // 보스는 마지막 스테이지에서만 등장하므로, 보스가 죽었다는 건 곧 던전 클리어
            ClearStage().Forget();
            return;
        }

        --m_iRemainMonsterCount;
        if (m_iRemainMonsterCount <= 0 && m_refSpawner.RemainObject <= 0)
            SpawnBossWhenCameraFree().Forget();
    }

    // 핵폭탄처럼 몬스터를 한 번에 전멸시키는 연출 도중이면, 그 컷신이 끝난 뒤에 보스 등장 컷신을 시작
    // (둘 다 CameraManager.MoveToPoint를 쓰므로 겹치면 뒤에 시작한 쪽이 카메라를 가로챔)
    private async UniTaskVoid SpawnBossWhenCameraFree()
    {
        await UniTask.WaitUntil(() => CameraManager.m_Instance.IsLocked == false, PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
        SpawnBoss();
    }

    // 그 스테이지의 일반 몬스터를 다 처치했을 때 호출: 마지막 스테이지면 보스 등장, 아니면 다음 스테이지로
    
    private void SpawnBoss()
    {
        m_bBossSpawned = true;

        SOStage refStage = m_listStage[m_iCurrentStageIdx];
        m_refSpawner.AddSpawnObject(0.0f, refStage.BossPrefab, refStage.BossSpawnPosition);

        // 등장 연출 방향은 스폰된 인스턴스가 아니라 원본 프리팹의 forward를 그대로 씀
        PoolObject refBossPoolObj = ObjectPoolManager.m_Instance.GetPoolPrefab(refStage.BossPrefab);
        Vector3 vBossForward = refBossPoolObj != null ? refBossPoolObj.transform.forward : Vector3.forward;
        Vector3 vCamTargetPos = refStage.BossSpawnPosition + (vBossForward.normalized * refStage.BossShowDistance);
        Quaternion qCamTargetRot = Quaternion.LookRotation(-vBossForward.normalized);

        CameraManager.m_Instance.MoveToPoint(
            this.GetCancellationTokenOnDestroy(),vCamTargetPos,qCamTargetRot,3.0f,2.0f).Forget();


        Time.timeScale = 0.0f;
    }


    private async UniTaskVoid ClearStage()
    {
        await UniTask.WaitForSeconds(5); 
        GameSceneManager.m_Instance.LoadFirstScene();
    }
}
