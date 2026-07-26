using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameSceneManager : MonoBehaviour
{
    [SerializeField] private SOSceneData m_refSceneData;

    private async UniTaskVoid Start()
    {
        if (m_refSceneData == null)
        {
            Debug.Log("씬 데이터 미설정 : SceneController");
            return;
        }

        await ObjectPool.m_Instance.LoadPoolAsync(m_refSceneData.PoolDataList, this.GetCancellationTokenOnDestroy());

        Debug.Log("로딩완료");
    }
}
