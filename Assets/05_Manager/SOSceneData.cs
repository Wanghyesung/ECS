using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "SO_SceneData", menuName = "Game/Load/SceneData")]
public class SOSceneData : ScriptableObject
{
    public AssetReference SceneAddress; //Addressable로 등록된 씬 주소 (씬 하나당 SOSceneData 하나)

    public List<SOPoolData> PoolDataList = new List<SOPoolData>();
}
