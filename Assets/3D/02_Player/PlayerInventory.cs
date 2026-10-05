using System;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                PlayerInventory
목적 : 보유 장비와 장착 장비의 ID를 관리하고 획득·장착 시 저장을 요청한다.
      로비 UI를 복원하고 장착 장비의 스탯을 PlayerPreLoadData에 반영한다.
 *///////////////////////////////////////////

public sealed class PlayerInventory : ISaveLoadable
{
    [Serializable]
    private sealed class InventoryData
    {
        [SerializeField] private List<string> m_listInventoryItemIds = new List<string>();
        [SerializeField] private string[] m_arrEquippedItemIds = new string[(int)eEquipType.Shoes + 1];

        public List<string> InventoryItemIds { get => m_listInventoryItemIds; set => m_listInventoryItemIds = value; }
        public string[] EquippedItemIds { get => m_arrEquippedItemIds; set => m_arrEquippedItemIds = value; }
    }

    private readonly ProfileSave m_refSaveManager;
    private readonly PlayerPreLoadData m_refPreLoad;
    private readonly SOEquipCatalog m_refCatalog;
    private readonly List<string> m_listInventoryItemIds = new List<string>();
    private readonly string[] m_arrEquippedItemIds = new string[(int)eEquipType.Shoes + 1];
    private readonly List<tStatValue> m_listEquipmentStats = new List<tStatValue>();
    private readonly InventoryData m_refData = new InventoryData();

    public string FileName => "inventory.json";

    public PlayerInventory(ProfileSave _refSaveManager, PlayerPreLoadData _refPreLoad, SOEquipCatalog _refCatalog)
    {
        m_refSaveManager = _refSaveManager;
        m_refPreLoad = _refPreLoad;
        m_refCatalog = _refCatalog;

        if (m_refCatalog == null)
            return;
        IReadOnlyList<SOEqipData> listStartingItems = m_refCatalog.StartingItems;
        for (int i = 0; i < listStartingItems.Count; ++i)
        {
            SOEqipData refItem = listStartingItems[i];
            if (refItem != null && string.IsNullOrEmpty(refItem.SaveId) == false)
                m_listInventoryItemIds.Add(refItem.SaveId);
        }
    }

    public bool Load(string _strJson)
    {
        if (_strJson == null || _strJson.IndexOf("\"m_listInventoryItemIds\"", StringComparison.Ordinal) < 0)
            return false;
        if (_strJson.IndexOf("\"m_arrEquippedItemIds\"", StringComparison.Ordinal) < 0)
            return false;

        InventoryData refData = new InventoryData
        {
            InventoryItemIds = null,
            EquippedItemIds = null,
        };
        try
        {
            JsonUtility.FromJsonOverwrite(_strJson, refData);
        }
        catch (Exception)
        {
            return false;
        }

        if (refData.InventoryItemIds == null)
            return false;
        if (refData.EquippedItemIds == null || refData.EquippedItemIds.Length != m_arrEquippedItemIds.Length)
            return false;

        m_listInventoryItemIds.Clear();
        m_listInventoryItemIds.AddRange(refData.InventoryItemIds);
        Array.Copy(refData.EquippedItemIds, m_arrEquippedItemIds, m_arrEquippedItemIds.Length);
        ApplyEquipmentStats();
        return true;
    }

    public string Save()
    {
        m_refData.InventoryItemIds = m_listInventoryItemIds;
        m_refData.EquippedItemIds = m_arrEquippedItemIds;
        return JsonUtility.ToJson(m_refData);
    }

    public bool CanRegisterItem(SOEqipData _refItem)
    {
        if (_refItem == null || m_refCatalog == null)
            return false;
        if (string.IsNullOrEmpty(_refItem.SaveId))
            return false;
        return m_refCatalog.Find(_refItem.SaveId) != null;
    }

    // 상점 구매와 이후 스테이지 보상/드롭 획득이 공통으로 쓰는 진입점.
    public bool AcquireItem(SOEqipData _refItem)
    {
        if (CanRegisterItem(_refItem) == false)
        {
            Debug.LogError("카탈로그에 등록되지 않은 장비는 저장할 수 없습니다.");
            return false;
        }

        m_listInventoryItemIds.Add(_refItem.SaveId);
        m_refSaveManager.Save(this);
        return true;
    }

    public bool EquipItem(SOEqipData _refItem, SOEqipData _refPrevious)
    {
        if (CanEquipItem(_refItem) == false)
            return false;
        int iSlot = (int)_refItem.EquipType;
        if (m_listInventoryItemIds.Remove(_refItem.SaveId) == false)
            return false;

        if (_refPrevious != null)
            m_listInventoryItemIds.Add(_refPrevious.SaveId);
        m_arrEquippedItemIds[iSlot] = _refItem.SaveId;
        ApplyEquipmentStats();
        m_refSaveManager.Save(this);
        return true;
    }

    public bool CanEquipItem(SOEqipData _refItem)
    {
        if (CanRegisterItem(_refItem) == false)
            return false;
        int iSlot = (int)_refItem.EquipType;
        return iSlot >= 0 && iSlot < m_arrEquippedItemIds.Length
            && m_listInventoryItemIds.Contains(_refItem.SaveId);
    }

    public void RestoreInventory(Container _refInventory)
    {
        if (m_refCatalog == null)
            return;

        int iCategory = _refInventory.GetCategoryIdx(eDataType.Equip);
        if (iCategory < 0)
            return;

        List<SOData> listInventory = _refInventory.GetListData(eDataType.Equip);
        if (m_listInventoryItemIds.Count > listInventory.Count)
            _refInventory.Resize(m_listInventoryItemIds.Count, eDataType.Equip);
        _refInventory.ClearData(iCategory);
        for (int i = 0; i < m_listInventoryItemIds.Count; ++i)
        {
            SOEqipData refItem = m_refCatalog.Find(m_listInventoryItemIds[i]);
            if (refItem == null)
            {
                Debug.LogWarning($"세이브의 장비 ID를 찾을 수 없습니다: {m_listInventoryItemIds[i]}");
                continue;
            }
            if (_refInventory.AddData(refItem) == false)
                Debug.LogWarning("인벤토리 슬롯이 부족해 일부 저장 장비가 표시되지 않았습니다.");
        }
    }

    public void RestoreEquipment(Interface _refEquipment)
    {
        if (m_refCatalog == null)
            return;

        for (int i = 0; i < m_arrEquippedItemIds.Length; ++i)
        {
            string strId = m_arrEquippedItemIds[i];
            if (string.IsNullOrEmpty(strId))
                continue;

            SOEqipData refItem = m_refCatalog.Find(strId);
            if (refItem == null)
            {
                Debug.LogWarning($"세이브의 장착 장비 ID를 찾을 수 없습니다: {strId}");
                continue;
            }
            if ((int)refItem.EquipType != i)
            {
                Debug.LogWarning($"장착 슬롯과 장비 종류가 다릅니다: {strId}");
                continue;
            }
            if (_refEquipment.AddData(refItem) == false)
                Debug.LogWarning($"장비 슬롯 복원 실패: {strId}");
        }
    }

    private void ApplyEquipmentStats()
    {
        m_listEquipmentStats.Clear();
        if (m_refCatalog != null)
        {
            for (int i = 0; i < m_arrEquippedItemIds.Length; ++i)
            {
                SOEqipData refItem = m_refCatalog.Find(m_arrEquippedItemIds[i]);
                if (refItem == null)
                    continue;

                IReadOnlyList<tStatValue> listStats = refItem.ListValue;
                for (int j = 0; j < listStats.Count; ++j)
                    m_listEquipmentStats.Add(listStats[j]);
            }
        }
        m_refPreLoad.SetEquipmentStats(m_listEquipmentStats);
    }
}
