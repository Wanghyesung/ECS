using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;

/*///////////////////////////////////////////
                DungeonManager
기능 : 던전 한 판의 진행(몬스터 스폰 예약, 보스 등장, 클리어)을 책임지는 매니저.
       m_listStage는 '순서대로 도는 코스'가 아니라 로비(SelectStage)에서 고르는
       난이도 목록이다 - StartStage(idx)로 그 하나만 시작하고, 잡몹을 전부 처치하면
       그 스테이지의 보스가 등장, 보스를 잡으면 한 판이 끝나 로비로 돌아간다.
 *///////////////////////////////////////////
public class DungeonManager : MonoBehaviour
{
    public static DungeonManager m_Instance = null;

    [SerializeField] private ObjectSpawner m_refSpawner;
    //[SerializeField] private List<SOStage> m_listStage = new List<SOStage>();

    private SOStage m_SOTargetStage = null;

    // 예약 수가 아니라 '실제로 스폰돼서 아직 살아있는 수
    private int m_iAliveMonsterCount = 0;

    // 보스 등장 '요청'과 '실제 등장'을 나눠서 본다.
    private bool m_bBossRequested = false;
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
        Player.OnPlayerDied.Subscribe(_ => ClearStage().Forget()).AddTo(ref m_bagEvents);   // 사망도 클리어와 같은 출구(5초 → 로비)
    }

    private void OnDisable()
    {
        m_bagEvents.Clear();
    }

    public void StartStage(SOStage _SOStage)
    {
        if (_SOStage == null)
        {
            Debug.LogError("스테이지에 맞는 데이터를 설정하세요");
            GameSceneManager.m_Instance.LoadFirstScene();
            return;
        }

        m_SOTargetStage = _SOStage;

        m_bBossSpawned = false;

        m_iAliveMonsterCount = 0;
        m_bBossRequested = false;

        m_listSpawnedMonster.Clear();
        m_refSpawner.Clear();   // 이전 런 예약이 남아 있으면 안 됨 (스포너는 DDOL)
        for (int i = 0; i < m_SOTargetStage.ListSpawnEntry.Count; ++i)
        {
            var tEntry = m_SOTargetStage.ListSpawnEntry[i];
            m_refSpawner.AddSpawnObject(tEntry.fSpawnTime, tEntry.MonsterPrefab, tEntry.vPosition);
        }
    }

    // 풀 재사용으로 같은 인스턴스가 다시 스폰될 수 있어 Contains로 중복 방지 (스폰 빈도가 낮아 O(n) 허용)
    private void HandleSpawned(GameObject _refSpawned)
    {
        if (_refSpawned.TryGetComponent(out Monster refMonster) == false)
            return;

        if (m_bBossSpawned == false)
            ++m_iAliveMonsterCount;

        if (m_listSpawnedMonster.Contains(refMonster) == false)
            m_listSpawnedMonster.Add(refMonster);
    }

    public void GetMonsters(List<Monster> _listBuffer)
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
            // 한 판에 보스는 하나뿐이므로, 보스가 죽었다는 건 곧 던전 클리어
            ClearStage().Forget();
            return;
        }

        --m_iAliveMonsterCount;
        if (m_bBossRequested == true)
            return;

        if (m_iAliveMonsterCount <= 0 && m_refSpawner.RemainObject <= 0)
        {
            m_bBossRequested = true;
            SpawnBossWhenCameraFree().Forget();
        }
    }

    // 핵폭탄처럼 몬스터를 한 번에 전멸시키는 연출 도중이면, 그 컷신이 끝난 뒤에 보스 등장 컷신을 시작
    // (둘 다 CameraManager.MoveToPoint를 쓰므로 겹치면 뒤에 시작한 쪽이 카메라를 가로챔)
    private async UniTaskVoid SpawnBossWhenCameraFree()
    {
        await UniTask.WaitUntil(() => CameraManager.m_Instance.IsLocked == false, PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
        SpawnBoss().Forget();
    }

    // 그 스테이지의 일반 몬스터를 다 처치했을 때 호출: 마지막 스테이지면 보스 등장, 아니면 다음 스테이지로
    
    private async UniTaskVoid SpawnBoss()
    {
        m_bBossSpawned = true;

        m_refSpawner.AddSpawnObject(0.0f, m_SOTargetStage.BossPrefab, m_SOTargetStage.BossSpawnPosition);

        // 등장 연출 방향은 스폰된 인스턴스가 아니라 원본 프리팹의 forward를 그대로 씀
        PoolObject refBossPoolObj = ObjectPoolManager.m_Instance.GetPoolPrefab(m_SOTargetStage.BossPrefab);
        Vector3 vBossForward = refBossPoolObj != null ? refBossPoolObj.transform.forward : Vector3.forward;
        Vector3 vCamTargetPos = m_SOTargetStage.BossSpawnPosition + (vBossForward.normalized * m_SOTargetStage.BossShowDistance);
        Quaternion qCamTargetRot = Quaternion.LookRotation(-vBossForward.normalized);

        // 등장 컷신 동안 정지. 컷신 중 레벨업 카드가 열리고 닫혀도 여기 Pause 가 남아 있어 컷신이 끝나기 전엔 풀리지 않는다
        // (카메라는 시간을 건드리지 않으므로 복귀도 여기서 - 예전엔 MoveToPoint 가 끝에서 timeScale=1 을 대신 썼다)
        TimeScaleManager.m_Instance.Pause(this);

        await CameraManager.m_Instance.MoveToPoint(
            this.GetCancellationTokenOnDestroy(), vCamTargetPos, qCamTargetRot, 3.0f, 2.0f);

        TimeScaleManager.m_Instance.Resume(this);
    }


    private async UniTaskVoid ClearStage()
    {
        m_refSpawner.Clear();   // 런 종료 — 남은 예약이 씬 전환 뒤 파괴된 풀을 건드리지 않게 (검증 중 실제 발생: 스포너 루프 사망 → 다음 런 몬스터 0)
        await UniTask.WaitForSeconds(5);
        GameSceneManager.m_Instance.LoadFirstScene();
    }
}
