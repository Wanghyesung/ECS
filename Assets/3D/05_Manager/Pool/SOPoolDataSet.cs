using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_PoolDataSet", menuName = "Game/Load/PoolData Set")]
public sealed class SOPoolDataSet : ScriptableObject
{
    [SerializeField] private List<SOPoolData> m_listPoolData = new List<SOPoolData>();

    public IReadOnlyList<SOPoolData> PoolDataList => m_listPoolData;
}
