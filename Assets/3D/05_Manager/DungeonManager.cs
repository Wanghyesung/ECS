using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using R3;
using UnityEngine;

/*///////////////////////////////////////////
                DungeonManager
목적 : 던전의 몬스터 스폰·보스 등장·클리어 보상과 종료를 관리한다.
 *///////////////////////////////////////////
public class DungeonManager : MonoBehaviour
{
    public static DungeonManager m_Instance = null;

    [SerializeField] private ObjectSpawner m_refSpawner;
    [SerializeField] private ClearStageUI m_refClearUI;
    //[SerializeField] private List<SOStage> m_listStage = new List<SOStage>();

    private SOStage m_SOTargetStage = null;
    private readonly List<SOEqipData> m_listClearRewards = new List<SOEqipData>(16);
    private bool m_bStageEnding;

    // 예약 수가 아니라 '실제로 스폰돼서 아직 살아있는 수
    private int m_iAliveMonsterCount = 0;

    // 보스 등장 '요청'과 '실제 등장'을 나눠서 본다.
    private bool m_bBossRequested = false;
    private bool m_bBossSpawned = false;

    // 스포너가 꺼낸 몬스터 목록(보스 포함)
    private List<Monster> m_listSpawnedMonster = new List<Monster>();

    // 보스 등장 컷신 진행 중 여부. 시작/끝을 한 스트림으로 알려야 View가 컷신 동안만 경고를 띄울 수 있음
    private readonly ReactiveProperty<bool> m_rpIsBossCutscene = new ReactiveProperty<bool>(false);
    public ReadOnlyReactiveProperty<bool> IsBossCutscene => m_rpIsBossCutscene;

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
        Player.OnPlayerDied.Subscribe(_ => FailStage().Forget()).AddTo(ref m_bagEvents);
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
        m_bStageEnding = false;
        m_refClearUI.Initialize();

        m_bBossSpawned = false;

        m_iAliveMonsterCount = 0;
        m_bBossRequested = false;

        m_listSpawnedMonster.Clear();
        m_refSpawner.Clear();
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
            ClearStage();
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
        Vector3 vCamTargetPos = m_SOTargetStage.BossSpawnPosition
                              + (vBossForward.normalized * m_SOTargetStage.BossShowDistance)
                              + (Vector3.up * m_SOTargetStage.BossShowHeight);
        Quaternion qCamTargetRot = Quaternion.LookRotation(m_SOTargetStage.BossSpawnPosition - vCamTargetPos);

        // 등장 컷신 동안 정지. 컷신 중 레벨업 카드가 열리고 닫혀도 여기 Pause 가 남아 있어 컷신이 끝나기 전엔 풀리지 않는다
        // (카메라는 시간을 건드리지 않으므로 복귀도 여기서 - 예전엔 MoveToPoint 가 끝에서 timeScale=1 을 대신 썼다)
        TimeScaleManager.m_Instance.Pause(this);
        m_rpIsBossCutscene.Value = true;

        try
        {
            await CameraManager.m_Instance.MoveToPoint(
                this.GetCancellationTokenOnDestroy(), vCamTargetPos, qCamTargetRot, 3.0f, 2.0f);
        }
        finally
        {
            m_rpIsBossCutscene.Value = false;
        }

        TimeScaleManager.m_Instance.Resume(this);
    }


    private void ClearStage()
    {
        if (m_bStageEnding == true)
            return;

        m_bStageEnding = true;
        m_refSpawner.Clear();
        TimeScaleManager.m_Instance.Pause(this);

        SOClearReward refReward = m_SOTargetStage.ClearReward;
        ProfileSave refSave = ProfileSave.m_Instance;
        int iStartExp = refSave.Level.TotalExp;
        refSave.Level.AddExp(refReward.ClearExp);
        m_listClearRewards.Clear();
        for (int i = 0; i < refReward.RewardCount; ++i)
        {
            SOEqipData refItem = refReward.Roll();
            if (refSave.Inventory.AcquireItem(refItem) == false)
                throw new System.InvalidOperationException($"{m_SOTargetStage.name}의 클리어 보상을 저장할 수 없습니다.");
            m_listClearRewards.Add(refItem);
        }

        refSave.Save();
        m_refClearUI.Show(GameSceneManager.m_Instance.SelectedStageIdx + 1,
            iStartExp, refSave.Level.TotalExp, m_listClearRewards);
    }

    private async UniTaskVoid FailStage()
    {
        if (m_bStageEnding == true)
            return;

        m_bStageEnding = true;
        ProfileSave.m_Instance.Save();
        m_refSpawner.Clear();
        await UniTask.WaitForSeconds(5, cancellationToken: this.GetCancellationTokenOnDestroy());
        GameSceneManager.m_Instance.LoadFirstScene();
    }
}
