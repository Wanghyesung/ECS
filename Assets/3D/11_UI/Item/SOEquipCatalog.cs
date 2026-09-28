using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                SOEquipCatalog
목적 : 저장된 장비 ID를 SOEqipData로 찾고 첫 실행의 시작 아이템을 제공한다.
      Resources의 EquipCatalog 에셋으로 빌드에 포함된다.
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "EquipCatalog", menuName = "Game/Item/Equip Catalog")]
public sealed class SOEquipCatalog : ScriptableObject
{
    [SerializeField] private List<SOEqipData> m_listItems = new List<SOEqipData>();
    [SerializeField] private List<SOEqipData> m_listStartingItems = new List<SOEqipData>();

    public IReadOnlyList<SOEqipData> Items => m_listItems;
    public IReadOnlyList<SOEqipData> StartingItems => m_listStartingItems;

    public SOEqipData Find(string _strId)
    {
        if (string.IsNullOrEmpty(_strId))
            return null;

        for (int i = 0; i < m_listItems.Count; ++i)
        {
            SOEqipData refItem = m_listItems[i];
            if (refItem != null && refItem.SaveId == _strId)
                return refItem;
        }

        return null;
    }
}
