using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "SO_PoolData", menuName = "Game/Load/PoolData")]
public class SOPoolData : ScriptableObject
{
    public AssetReferenceGameObject PrefabRef;
    public int PreLoad = 8;
    public int Max = 12; //아직 사용하지 않음

    // 동시 활성 개수 상한. 이 값보다 많이 GetObject되면 가장 오래 활성 상태였던 인스턴스를
    // 강제로 반납해 자리를 만든다. 0 이하면 상한 없음(기존 동작과 동일)
    public int ActiveCap = -1;
}
