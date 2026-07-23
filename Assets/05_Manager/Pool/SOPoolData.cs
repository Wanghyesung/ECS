using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "SO_PoolData", menuName = "Game/Load/PoolData")]
public class SOPoolData : ScriptableObject
{
    public AssetReferenceGameObject PrefabRef;
    public int PreLoad = 8;
    public int Max = 12; //아직 사용하지 않음
}
