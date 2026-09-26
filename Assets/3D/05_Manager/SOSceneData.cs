using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "SO_SceneData", menuName = "Game/Load/SceneData")]
public class SOSceneData : ScriptableObject
{
    public AssetReference SceneAddress; //Addressable로 등록된 씬 주소 (씬 하나당 SOSceneData 하나)

    [FormerlySerializedAs("PoolDataList")]
    [SerializeField] private List<SOPoolData> m_listScenePoolData = new List<SOPoolData>();

    public IReadOnlyList<SOPoolData> ScenePoolDataList => m_listScenePoolData;

    public SOStage Stage;

    public SOAudio Bgm;   //이 씬에 들어갈 때 틀 배경음 (Loop SO). 비우면 이전 곡이 계속 재생됨
}
