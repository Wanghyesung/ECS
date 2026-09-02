using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

public class TestStartCode : MonoBehaviour
{
    [SerializeField] private SOSceneData SOData;
    // ObjectPoolManager.m_Instance는 ObjectPoolManager.Awake()에서 세팅됨.
    // 같은 씬에 있는 두 스크립트 간 Awake 호출 순서는 보장되지 않으므로,
    // 씬의 모든 Awake가 끝난 뒤 호출이 보장되는 Start에서 실행해야
    // ObjectPoolManager.m_Instance가 null인 상태로 LoadPoolAsync를 호출하는 걸 피할 수 있음.
    private void Start()
    {
        LoadSceneDataAsync(SOData).Forget();
    }


    private async UniTaskVoid LoadSceneDataAsync(SOSceneData _refSceneData)
    {

        await ObjectPoolManager.m_Instance.LoadPoolAsync(_refSceneData.PoolDataList, this.GetCancellationTokenOnDestroy());
        Debug.Log("로딩완료");
    }
   
}
