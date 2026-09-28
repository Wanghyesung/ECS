using System;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                PlayerProfile
목적 : 골드, 영구 스탯, 보유 장비 ID, 장착 장비 ID를 JSON으로 직렬화할 데이터.
      런타임 오브젝트 대신 값과 ID만 담아 다음 실행에서 복원한다.
 *///////////////////////////////////////////

[Serializable]
public sealed class PlayerProfile
{
    [SerializeField] private int m_iVersion = 1;
    [SerializeField] private int m_iGold = 5000;
    [SerializeField] private List<tStatValue> m_listPermanentStats = new List<tStatValue>();
    [SerializeField] private List<string> m_listInventoryItemIds = new List<string>();
    [SerializeField] private string[] m_arrEquippedItemIds = new string[(int)eEquipType.Shoes + 1];

    public int Version => m_iVersion;
    public int Gold { get => m_iGold; set => m_iGold = value; }
    public List<tStatValue> PermanentStats => m_listPermanentStats;
    public List<string> InventoryItemIds => m_listInventoryItemIds;
    public string[] EquippedItemIds => m_arrEquippedItemIds;
}
