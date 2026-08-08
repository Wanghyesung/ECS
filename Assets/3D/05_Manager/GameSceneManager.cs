using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*///////////////////////////////////////////
                GameSceneManager
기능 : 로비(SelectStage)에서 고른 스테이지 idx에 대응하는 SOSceneData를 찾아
      Addressable 씬을 비동기로 로드하고, 진행률을 이미지(fillAmount)로 보여준다.
      씬 전환 도중에도 살아있어야 해서 로비에서 생성되는 DontDestroyOnLoad
      싱글톤으로 유지하고, 씬 로드가 끝나면 그 씬에서 쓸 오브젝트 풀 데이터까지 이어서 로드한다.
 *///////////////////////////////////////////
public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager m_Instance = null;

    //SelectStage의 이미지 idx와 1:1 대응. 각 스테이지가 어떤 씬 + 어떤 풀데이터를 쓰는지 여기서 관리
    [SerializeField] private List<SOSceneData> m_listSceneData = new List<SOSceneData>();

    //[SerializeField] private Image m_refProgressImage; //fillAmount로 로딩 진행률 표시
    [SerializeField] private LoadingOverlay m_refLoadingOverlay;
    [SerializeField] private string m_strFirstSceneName = "LobyScene"; //Addressable이 아닌 Build Settings 등록 씬(로비/처음 씬)

    public int SelectedStageIdx { get; private set; } = 0;

    private AsyncOperationHandle<SceneInstance> m_tSceneHandle;

    private void Awake()
    {
        if (m_Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(gameObject);

        if (m_refLoadingOverlay != null)
            m_refLoadingOverlay.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (m_Instance == this)
            m_Instance = null;
    }

    //SelectStage에서 이미지를 클릭했을 때 호출
    public void LoadStage(int _iStageIdx)
    {
        if (_iStageIdx < 0 || _iStageIdx >= m_listSceneData.Count)
        {
            Debug.Log("잘못된 스테이지 idx : GameSceneManager");
            return;
        }

        SelectedStageIdx = _iStageIdx;
        LoadSceneAsync(m_listSceneData[_iStageIdx]).Forget();
    }

    private async UniTaskVoid LoadSceneAsync(SOSceneData _refSceneData)
    {
        if (m_refLoadingOverlay != null)
            m_refLoadingOverlay.gameObject.SetActive(true);

        SetProgress(0.0f);
        //씬 로드가 끝났다고 100%가 되면 안 되므로(뒤에 풀 로딩이 남음) 구간을 절반씩 나눠서 표시
        //씬 로드 0~0.5, 풀 로드 0.5~1.0
        var refSceneProgress = Progress.Create<float>(fPercent => SetProgress(fPercent * 0.5f));
        var refPoolProgress = Progress.Create<float>(fPercent => SetProgress(0.5f + fPercent * 0.5f));

        m_tSceneHandle = Addressables.LoadSceneAsync(_refSceneData.SceneAddress, LoadSceneMode.Single);
        await m_tSceneHandle.ToUniTask(refSceneProgress, cancellationToken: this.GetCancellationTokenOnDestroy());

        await ObjectPool.m_Instance.LoadPoolAsync(_refSceneData.PoolDataList, this.GetCancellationTokenOnDestroy(), refPoolProgress);

        if (m_refLoadingOverlay != null)
            m_refLoadingOverlay.gameObject.SetActive(false);

        DungeonManager.m_Instance.StartStage(SelectedStageIdx);
        Debug.Log("로딩완료");
    }

    private void SetProgress(float _fPercent)
    {
        m_refLoadingOverlay.SetProgress(_fPercent);

    }

    //던전 클리어 등으로 처음 씬(로비)으로 돌아갈 때 호출. 로비는 Addressable이 아니라 Build Settings에 등록된 일반 씬이라 SceneManager로 바로 로드
    public void LoadFirstScene()
    {
        LoadFirstSceneAsync().Forget();
    }

    private async UniTaskVoid LoadFirstSceneAsync()
    {
        //진행률 계산 없이 배경(오버레이)만 켰다 끄기
        if (m_refLoadingOverlay != null)
            m_refLoadingOverlay.ShowLoadingImage();

        await SceneManager.LoadSceneAsync(m_strFirstSceneName, LoadSceneMode.Single)
            .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

        if (m_refLoadingOverlay != null)
            m_refLoadingOverlay.CompletedLoading();
    }
}
