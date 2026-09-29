using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_ClearReward", menuName = "Game/Dungeon/Clear Reward")]
public sealed class SOClearReward : ScriptableObject
{
    [Serializable]
    public sealed class RewardItem
    {
        [SerializeField] private SOEqipData m_refItem;
        [SerializeField, Min(1)] private int m_iWeight = 1;
        public SOEqipData Item => m_refItem;
        public int Weight => Mathf.Max(1, m_iWeight);
    }

    [SerializeField, Min(1)] private int m_iRewardCount = 4;
    [SerializeField, Min(0)] private int m_iClearExp = 100;
    [SerializeField] private List<RewardItem> m_listEntries = new List<RewardItem>();

    public int RewardCount => Mathf.Clamp(m_iRewardCount, 1, 16);
    public int ClearExp => Mathf.Max(0, m_iClearExp);

    public SOEqipData Roll()
    {
        int iTotalWeight = 0;
        for (int i = 0; i < m_listEntries.Count; ++i)
            if (m_listEntries[i].Item != null)
                iTotalWeight += m_listEntries[i].Weight;

        if (iTotalWeight == 0)
            return null;

        int iRoll = UnityEngine.Random.Range(0, iTotalWeight);
        for (int i = 0; i < m_listEntries.Count; ++i)
        {
            RewardItem tEntry = m_listEntries[i];
            if (tEntry.Item == null)
                continue;
            iRoll -= tEntry.Weight;
            if (iRoll < 0)
                return tEntry.Item;
        }
        return null;
    }
}
