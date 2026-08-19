using Cysharp.Threading.Tasks;
using UnityEngine;

/*///////////////////////////////////////////
              TestSceneLoader
목적 : Loby -> GameSceneManager.LoadStage() 흐름을 거치지 않고 씬을 곧바로 재생(Play)할
       때, 원래 그 흐름이 해줬을 SOSceneData.PoolDataList 로딩을 대신 해주는 테스트
       전용 부트스트랩. ObjectPool이 씬에 없으면 직접 만들어서 씬을 그 자리에서 바로
       테스트할 수 있게 한다.

       DefaultExecutionOrder로 다른 오브젝트들보다 Awake를 먼저 돌게 해서 로딩을 최대한
       일찍 "시작"은 시키지만, 로딩 자체가 비동기(UniTask)라 완료 시점까지 보장하진
       않는다 - 풀에서 바로 꺼내 쓰는 오브젝트가 씬 시작 직후에 있다면 그쪽에서 대기가
       필요할 수 있음(이 스크립트는 테스트 편의용이지, 로딩 순서를 완전히 보장하는
       장치는 아님).
 *///////////////////////////////////////////
[DefaultExecutionOrder(-10000)]
public class TestSceneLoader : MonoBehaviour
{
    [SerializeField] private SOSceneData m_refSceneData;

    private async UniTaskVoid Awake()
    {
        if (m_refSceneData == null)
        {
            Debug.LogWarning("TestSceneLoader: SOSceneData가 비어있음 - 인스펙터에서 지정 필요");
            return;
        }

        if (ObjectPool.m_Instance == null)
        {
            GameObject refPoolObj = new GameObject("ObjectPool");
            refPoolObj.AddComponent<ObjectPool>();
        }

        await ObjectPool.m_Instance.LoadPoolAsync(m_refSceneData.PoolDataList, this.GetCancellationTokenOnDestroy());
        Debug.Log("TestSceneLoader: 풀 로딩 완료 (" + m_refSceneData.PoolDataList.Count + "개)");
    }
}
