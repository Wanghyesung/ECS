---
name: inventory-system
description: "인벤토리, 장비, 제작(crafting) 패턴 — ScriptableObject 아이템 정의, 슬롯 기반 인벤토리, 장비 시스템, 제작 레시피, UI 바인딩. 아이템 관리를 구현할 때 로드할 것."
globs: ["**/Inventory*.cs", "**/Item*.cs", "**/Equipment*.cs", "**/Craft*.cs"]
---

# 인벤토리, 장비, 제작 시스템

완전한 아이템 관리 파이프라인을 구축하기 위한 패턴 모음: 아이템을 ScriptableObject로 정의하고, 슬롯 기반 인벤토리에 저장하고, 장비를 착용하고, 새 아이템을 제작하고, 이 모든 것을 UI에 바인딩한다.

## 아이템 정의 (ScriptableObject)

게임의 모든 아이템은 ScriptableObject 에셋으로 정의된다. 이렇게 하면 데이터가 코드 밖으로 분리되어 디자이너가 에디터에서 아이템을 만들 수 있고, 저장/로드도 단순해진다(전체 오브젝트가 아니라 ID로 참조하기 때문).

```csharp
using UnityEngine;

public enum ItemType
{
    Consumable,
    Equipment,
    Material,
    QuestItem,
    Currency
}

public enum EquipmentSlotType
{
    None,
    Head,
    Body,
    Weapon,
    Shield,
    Accessory
}

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item Definition")]
public sealed class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string itemId;           // 저장/로드용 고유 ID: "sword_iron_01"
    public string displayName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;

    [Header("Stacking")]
    public bool isStackable = true;
    public int maxStackSize = 99;

    [Header("Type")]
    public ItemType itemType;
    public EquipmentSlotType equipSlot = EquipmentSlotType.None;

    [Header("Stats (for equipment)")]
    public int attackBonus;
    public int defenseBonus;
    public int healthBonus;
    public int speedBonus;

    [Header("Usage (for consumables)")]
    public int healAmount;
    public int manaRestoreAmount;

    [Header("Economy")]
    public int buyPrice;
    public int sellPrice;
}
```

**itemId 네이밍 컨벤션:** 카테고리 접두사가 붙은 snake_case를 사용하라. 예: `sword_iron_01`, `potion_health_small`, `mat_wood_plank`. 이렇게 하면 세이브 파일을 사람이 읽기 쉬워지고 디버깅도 편해진다.

### 아이템 레지스트리 (Item Registry)

`itemId`로 `ItemDefinition`을 조회할 수 있도록 중앙 레지스트리를 유지하라. 저장/로드(ID 문자열을 저장했다가, 로드 시 참조를 재구성하는 방식)에 필수적이다.

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemRegistry", menuName = "Inventory/Item Registry")]
public sealed class ItemRegistry : ScriptableObject
{
    [SerializeField] private List<ItemDefinition> m_listAllItems;

    private Dictionary<string, ItemDefinition> m_hashLookup;

    public void Initialize()
    {
        m_hashLookup = new Dictionary<string, ItemDefinition>();
        foreach (ItemDefinition itemDef in m_listAllItems)
        {
            if (m_hashLookup.ContainsKey(itemDef.itemId))
            {
                Debug.LogWarning($"Duplicate item ID: {itemDef.itemId}");
                continue;
            }
            m_hashLookup[itemDef.itemId] = itemDef;
        }
    }

    public ItemDefinition GetById(string _strItemId)
    {
        if (m_hashLookup == null) Initialize();
        m_hashLookup.TryGetValue(_strItemId, out ItemDefinition itemDef);
        return itemDef;
    }
}
```

시작 시점에 모든 아이템을 레지스트리에 로드하거나, 명시적 리스트 대신 자동 탐색을 원한다면 `Resources.LoadAll<ItemDefinition>("Items/")`를 사용하라.

---

## 인벤토리 슬롯 (Inventory Slot)

각 슬롯은 아이템 정의에 대한 참조와 스택 수량을 가진다. 아이템이 null이면 빈 슬롯을 의미한다.

```csharp
using System;

[Serializable]
public sealed class InventorySlot
{
    public ItemDefinition item;
    public int count;

    public bool IsEmpty => item == null || count <= 0;

    public InventorySlot()
    {
        item = null;
        count = 0;
    }

    public InventorySlot(ItemDefinition _SOItem, int _iCount)
    {
        item = _SOItem;
        count = _iCount;
    }

    public void Clear()
    {
        item = null;
        count = 0;
    }
}
```

---

## Inventory 클래스

핵심 인벤토리: 고정 크기의 슬롯 배열과 추가/삭제/조회 메서드로 구성된다. 내용물이 바뀔 때마다 이벤트를 발생시켜 UI가 반응할 수 있게 한다.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class Inventory
{
    [SerializeField] private int m_iMaxSlots = 20;
    [SerializeField] private List<InventorySlot> m_listSlots;

    public int MaxSlots => m_iMaxSlots;
    public IReadOnlyList<InventorySlot> Slots => m_listSlots;

    /// <summary>
    /// 인벤토리 내용이 변경될 때마다 발생한다.
    /// int 매개변수는 변경된 슬롯 인덱스이다(대량 작업의 경우 -1).
    /// </summary>
    public event Action<int> OnChanged;

    public Inventory(int _iMaxSlots)
    {
        this.m_iMaxSlots = _iMaxSlots;
        m_listSlots = new List<InventorySlot>(_iMaxSlots);
        for (int i = 0; i < _iMaxSlots; i++)
            m_listSlots.Add(new InventorySlot());
    }

    /// <summary>
    /// 아이템을 추가한다. 추가하지 못한(오버플로) 개수를 반환한다.
    /// </summary>
    public int Add(ItemDefinition _SOItem, int _iAmount = 1)
    {
        if (_SOItem == null || _iAmount <= 0) return _iAmount;

        int iRemaining = _iAmount;

        // 1차: 같은 아이템을 가진 기존 슬롯에 스택으로 쌓는다
        if (_SOItem.isStackable)
        {
            for (int i = 0; i < m_listSlots.Count && iRemaining > 0; i++)
            {
                if (m_listSlots[i].item == _SOItem && m_listSlots[i].count < _SOItem.maxStackSize)
                {
                    int iSpaceInSlot = _SOItem.maxStackSize - m_listSlots[i].count;
                    int iToAdd = Mathf.Min(iRemaining, iSpaceInSlot);
                    m_listSlots[i].count += iToAdd;
                    iRemaining -= iToAdd;
                    OnChanged?.Invoke(i);
                }
            }
        }

        // 2차: 빈 슬롯에 배치한다
        for (int i = 0; i < m_listSlots.Count && iRemaining > 0; i++)
        {
            if (m_listSlots[i].IsEmpty)
            {
                int iToAdd = _SOItem.isStackable
                    ? Mathf.Min(iRemaining, _SOItem.maxStackSize)
                    : 1;
                m_listSlots[i].item = _SOItem;
                m_listSlots[i].count = iToAdd;
                iRemaining -= iToAdd;
                OnChanged?.Invoke(i);
            }
        }

        return iRemaining; // 0이면 전부 들어간 것이다
    }

    /// <summary>
    /// 아이템 수량을 제거한다. 실제로 제거된 개수를 반환한다.
    /// </summary>
    public int Remove(ItemDefinition _SOItem, int _iAmount = 1)
    {
        if (_SOItem == null || _iAmount <= 0) return 0;

        int iToRemove = _iAmount;

        for (int i = 0; i < m_listSlots.Count && iToRemove > 0; i++)
        {
            if (m_listSlots[i].item == _SOItem)
            {
                int iRemoveFromSlot = Mathf.Min(iToRemove, m_listSlots[i].count);
                m_listSlots[i].count -= iRemoveFromSlot;
                iToRemove -= iRemoveFromSlot;

                if (m_listSlots[i].count <= 0)
                    m_listSlots[i].Clear();

                OnChanged?.Invoke(i);
            }
        }

        return _iAmount - iToRemove;
    }

    public bool HasItem(ItemDefinition _SOItem, int _iAmount = 1)
    {
        return GetCount(_SOItem) >= _iAmount;
    }

    public int GetCount(ItemDefinition _SOItem)
    {
        int iTotal = 0;
        foreach (InventorySlot slot in m_listSlots)
        {
            if (slot.item == _SOItem)
                iTotal += slot.count;
        }
        return iTotal;
    }

    /// <summary>
    /// 두 슬롯의 내용을 교환한다(드래그 앤 드롭 재배치용).
    /// </summary>
    public void SwapSlots(int _iIndexA, int _iIndexB)
    {
        if (_iIndexA < 0 || _iIndexA >= m_listSlots.Count || _iIndexB < 0 || _iIndexB >= m_listSlots.Count) return;

        var slotTemp = new InventorySlot(m_listSlots[_iIndexA].item, m_listSlots[_iIndexA].count);
        m_listSlots[_iIndexA].item = m_listSlots[_iIndexB].item;
        m_listSlots[_iIndexA].count = m_listSlots[_iIndexB].count;
        m_listSlots[_iIndexB].item = slotTemp.item;
        m_listSlots[_iIndexB].count = slotTemp.count;

        OnChanged?.Invoke(_iIndexA);
        OnChanged?.Invoke(_iIndexB);
    }
}
```

---

## 장비 시스템 (Equipment System)

장비 슬롯은 일치하는 `EquipmentSlotType`의 아이템만 받는다. 아이템을 장착하면 인벤토리에서 제거되어 장비 슬롯에 놓이고, 해제하면 그 반대로 동작한다.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EquipmentSystem
{
    private Dictionary<EquipmentSlotType, ItemDefinition> m_hashEquipped = new();

    public event Action<EquipmentSlotType> OnEquipmentChanged;

    public ItemDefinition GetEquipped(EquipmentSlotType _slotType)
    {
        m_hashEquipped.TryGetValue(_slotType, out var itemDef);
        return itemDef;
    }

    /// <summary>
    /// 아이템을 장착한다. 기존에 장착되어 있던 아이템(또는 null)을 반환한다.
    /// 아이템을 인벤토리와 주고받는 것은 호출하는 쪽의 책임이다.
    /// </summary>
    public ItemDefinition Equip(ItemDefinition _SOItem)
    {
        if (_SOItem == null || _SOItem.equipSlot == EquipmentSlotType.None)
            return null;

        m_hashEquipped.TryGetValue(_SOItem.equipSlot, out var itemDefPrevious);
        m_hashEquipped[_SOItem.equipSlot] = _SOItem;

        OnEquipmentChanged?.Invoke(_SOItem.equipSlot);
        return itemDefPrevious;
    }

    public ItemDefinition Unequip(EquipmentSlotType _slotType)
    {
        if (!m_hashEquipped.TryGetValue(_slotType, out var itemDef)) return null;

        m_hashEquipped.Remove(_slotType);
        OnEquipmentChanged?.Invoke(_slotType);
        return itemDef;
    }

    /// <summary>
    /// 장착된 모든 아이템의 스탯 보너스를 합산한다.
    /// </summary>
    public (int attack, int defense, int health, int speed) GetTotalBonuses()
    {
        int iAtk = 0, iDef = 0, iHp = 0, iSpd = 0;
        foreach (var kvp in m_hashEquipped)
        {
            if (kvp.Value == null) continue;
            iAtk += kvp.Value.attackBonus;
            iDef += kvp.Value.defenseBonus;
            iHp += kvp.Value.healthBonus;
            iSpd += kvp.Value.speedBonus;
        }
        return (iAtk, iDef, iHp, iSpd);
    }
}
```

---

## 제작 시스템 (Crafting System)

제작 레시피는 필요한 재료와 결과물 아이템을 나열한 ScriptableObject다.

```csharp
using UnityEngine;

[System.Serializable]
public struct CraftingIngredient
{
    public ItemDefinition item;
    public int count;
}

[CreateAssetMenu(fileName = "New Recipe", menuName = "Inventory/Crafting Recipe")]
public sealed class CraftingRecipe : ScriptableObject
{
    public string recipeName;
    public CraftingIngredient[] ingredients;
    public ItemDefinition result;
    public int resultCount = 1;

    public bool CanCraft(Inventory _inventory)
    {
        foreach (var ingredient in ingredients)
        {
            if (!_inventory.HasItem(ingredient.item, ingredient.count))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 제작을 시도한다. 성공하면 true를 반환한다.
    /// </summary>
    public bool Craft(Inventory _inventory)
    {
        if (!CanCraft(_inventory)) return false;

        // 결과물이 들어갈 공간이 있는지 확인
        // (단순화된 예시: 실제 구현에서는 드라이런으로 Add 가능 여부를 먼저 검사해야 한다)

        // 재료 제거
        foreach (var ingredient in ingredients)
        {
            _inventory.Remove(ingredient.item, ingredient.count);
        }

        // 결과물 추가
        int iOverflow = _inventory.Add(result, resultCount);
        if (iOverflow > 0)
        {
            Debug.LogWarning($"Crafting overflow: {iOverflow} x {result.displayName} did not fit.");
            // 실제 구현에서는 초과분을 바닥에 드롭하거나 제작 자체를 롤백해야 한다
        }

        return true;
    }
}
```

---

## UI 바인딩

옵저버 패턴을 사용한다: 인벤토리가 `OnChanged`를 발생시키면 UI가 이를 구독해 변경된 슬롯을 갱신한다.

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class InventoryUI : MonoBehaviour
{
    [SerializeField] private Transform m_refSlotContainer;
    [SerializeField] private GameObject m_refSlotPrefab;

    private Inventory m_inventory;
    private InventorySlotUI[] m_slotUIs;

    public void Bind(Inventory _inventory)
    {
        // 기존 구독 해제
        if (m_inventory != null)
            m_inventory.OnChanged -= RefreshSlot;

        m_inventory = _inventory;
        m_inventory.OnChanged += RefreshSlot;

        RebuildAllSlots();
    }

    private void OnDestroy()
    {
        if (m_inventory != null)
            m_inventory.OnChanged -= RefreshSlot;
    }

    private void RebuildAllSlots()
    {
        // 기존 것 정리
        foreach (Transform child in m_refSlotContainer)
            Destroy(child.gameObject);

        m_slotUIs = new InventorySlotUI[m_inventory.MaxSlots];

        for (int i = 0; i < m_inventory.MaxSlots; i++)
        {
            var go = Instantiate(m_refSlotPrefab, m_refSlotContainer);
            m_slotUIs[i] = go.GetComponent<InventorySlotUI>();
            m_slotUIs[i].SetSlotIndex(i);
            RefreshSlot(i);
        }
    }

    private void RefreshSlot(int _iIndex)
    {
        if (_iIndex < 0 || _iIndex >= m_slotUIs.Length) return;

        var slot = m_inventory.Slots[_iIndex];
        m_slotUIs[_iIndex].UpdateDisplay(slot.item, slot.count);
    }
}
```

### 슬롯 UI 컴포넌트

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class InventorySlotUI : MonoBehaviour
{
    [SerializeField] private Image m_refIconImage;
    [SerializeField] private TextMeshProUGUI m_refCountText;
    [SerializeField] private GameObject m_refCountBackground;

    private int m_iSlotIndex;

    public void SetSlotIndex(int _iIndex) => m_iSlotIndex = _iIndex;

    public void UpdateDisplay(ItemDefinition _SOItem, int _iCount)
    {
        if (_SOItem == null)
        {
            m_refIconImage.enabled = false;
            m_refCountBackground.SetActive(false);
            return;
        }

        m_refIconImage.enabled = true;
        m_refIconImage.sprite = _SOItem.icon;

        bool bShowCount = _SOItem.isStackable && _iCount > 1;
        m_refCountBackground.SetActive(bShowCount);
        if (bShowCount)
            m_refCountText.text = _iCount.ToString();
    }
}
```

---

## 드래그 앤 드롭

UGUI 기반 드래그 앤 드롭을 위해, 슬롯 UI에 드래그 핸들러 인터페이스를 구현하라.

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class InventorySlotDragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    private Canvas m_refCanvas;
    private RectTransform m_refRectTransform;
    private CanvasGroup m_refCanvasGroup;
    private Transform m_refOriginalParent;
    private int m_iSlotIndex;

    private static InventorySlotDragHandler m_refDraggingSlot;

    private void Awake()
    {
        m_refRectTransform = GetComponent<RectTransform>();
        m_refCanvasGroup = GetComponent<CanvasGroup>();
        m_refCanvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData _eventData)
    {
        m_refDraggingSlot = this;
        m_refCanvasGroup.blocksRaycasts = false; // 드롭 타겟이 이벤트를 받을 수 있도록 허용
        m_refCanvasGroup.alpha = 0.6f;
        m_refOriginalParent = transform.parent;
        transform.SetParent(m_refCanvas.transform); // 캔버스 최상단으로 이동
    }

    public void OnDrag(PointerEventData _eventData)
    {
        m_refRectTransform.anchoredPosition += _eventData.delta / m_refCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData _eventData)
    {
        m_refCanvasGroup.blocksRaycasts = true;
        m_refCanvasGroup.alpha = 1f;
        transform.SetParent(m_refOriginalParent);
        transform.localPosition = Vector3.zero;
        m_refDraggingSlot = null;
    }

    public void OnDrop(PointerEventData _eventData)
    {
        if (m_refDraggingSlot == null || m_refDraggingSlot == this) return;

        // 인벤토리에 두 슬롯을 교환하라고 알린다
        // (매니저나 이벤트를 통해 인벤토리에 접근한다)
        InventoryManager.Instance.SwapSlots(m_refDraggingSlot.m_iSlotIndex, m_iSlotIndex);
    }
}
```

---

## 아이템 픽업

트리거 콜라이더를 가진 픽업 오브젝트를 월드에 배치한다. 플레이어가 진입하면 아이템을 인벤토리에 추가하고 픽업 오브젝트를 제거한다.

```csharp
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class ItemPickup : MonoBehaviour
{
    [SerializeField] private ItemDefinition m_SOItem;
    [SerializeField] private int m_iAmount = 1;
    [SerializeField] private bool m_bDestroyOnPickup = true;

    private void OnTriggerEnter2D(Collider2D _refOther)
    {
        if (!_refOther.CompareTag("Player")) return;

        var playerInventory = _refOther.GetComponent<PlayerInventoryHolder>();
        if (playerInventory == null) return;

        int iOverflow = playerInventory.Inventory.Add(m_SOItem, m_iAmount);

        if (iOverflow == 0 && m_bDestroyOnPickup)
        {
            Destroy(gameObject);
        }
        else if (iOverflow > 0 && iOverflow < m_iAmount)
        {
            // 일부만 픽업됨
            m_iAmount = iOverflow;
        }
        // overflow == amount라면 인벤토리가 가득 찬 것이므로 아무것도 하지 않는다
    }
}
```

---

## 저장/로드 연동

인벤토리를 (itemId, count) 쌍의 리스트로 직렬화하라. 로드 시 각 ID를 ItemRegistry에서 조회하여 ScriptableObject 참조를 재구성한다.

```csharp
[System.Serializable]
public struct InventorySaveData
{
    public string[] itemIds;
    public int[] counts;
}

// 저장 로직에서:
public InventorySaveData CaptureInventory(Inventory _inventory)
{
    var data = new InventorySaveData
    {
        itemIds = new string[_inventory.MaxSlots],
        counts = new int[_inventory.MaxSlots]
    };

    for (int i = 0; i < _inventory.MaxSlots; i++)
    {
        var slot = _inventory.Slots[i];
        data.itemIds[i] = slot.IsEmpty ? "" : slot.item.itemId;
        data.counts[i] = slot.count;
    }

    return data;
}

// 로드 로직에서:
public void RestoreInventory(Inventory _inventory, InventorySaveData _data, ItemRegistry _SORegistry)
{
    for (int i = 0; i < _data.itemIds.Length; i++)
    {
        if (string.IsNullOrEmpty(_data.itemIds[i])) continue;

        var itemDef = _SORegistry.GetById(_data.itemIds[i]);
        if (itemDef == null)
        {
            Debug.LogWarning($"Item ID not found in registry: {_data.itemIds[i]}");
            continue;
        }

        _inventory.Add(itemDef, _data.counts[i]);
    }
}
```

---

## 실전 팁

- **ItemDefinition은 가볍게 유지하라.** 런타임에만 존재하는 상태(내구도, 인챈트)는 SO 자체가 아니라 별도의 `ItemInstance` 래퍼 클래스에 담아라. SO는 공유 에셋이므로, 런타임에 SO를 수정하면 그것을 참조하는 모든 곳이 함께 바뀐다.
- **아이템 타입에 enum을 아껴서 사용하라.** 게임에 아이템 카테고리가 많다면, 경직된 enum 대신 태그 기반 시스템(List<string> tags)을 고려하라.
- **툴팁 시스템:** 어떤 `ItemDefinition`이든 마우스 오버 시 `displayName`과 `description`을 읽어오는 범용 툴팁을 만들어라. `IPointerEnterHandler` / `IPointerExitHandler`를 사용하라.
- **인벤토리 정렬**은 타입별로, 그다음 이름순으로 하라: `slots.OrderBy(s => s.item?.itemType).ThenBy(s => s.item?.displayName)`.
- **인벤토리 가득 참 피드백:** 인벤토리가 가득 찼는데 플레이어가 아이템을 주우려 하면 사운드를 재생하거나 UI를 깜빡이게 하라. 조용히 실패하면 버그처럼 느껴진다.
- **아이템 희귀도:** `Rarity` enum(Common, Uncommon, Rare, Epic, Legendary)과 색상 매핑을 추가하라. 슬롯 테두리나 아이템 이름에 색을 입혀라.
