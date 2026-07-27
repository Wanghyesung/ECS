using System;
using System.Collections.Generic;
using UnityEngine;

public enum eStatType
{
    HP,
    Attack,
    Defense,
    Speed,
    BulletSpeed,
    End,
}

public enum eEquipType
{
    Weapon,
    Gloves,
    Helmet,
    Shoes,
    End,
}

[Serializable]
public struct tStatValue
{
    public eStatType Type;
    public float Value;
}

/*///////////////////////////////////////////
                SOItemData
기능 : 아이템의 공용 데이터(SOData: 설명, 아이콘)를 확장한 아이템 전용 데이터.
      순수 데이터만 가지며, 실제 적용/사용 기능은 Inventory/PlayerInterface가 담당.
      능력치는 (eStatType, 값) 쌍의 리스트로 가져서, 적용부(PlayerInterface)가
      아이템 종류별 분기 없이 리스트를 순회하며 공용으로 처리할 수 있게 한다.
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_ItemData", menuName = "Game/Item/SOItemData")]
public class SOItemData : SOData
{
    [SerializeField] private eEquipType m_eEquipType = eEquipType.End;
    public eEquipType EquipType => m_eEquipType;

    [SerializeField] private List<tStatValue> m_listValue = new List<tStatValue>();
    public IReadOnlyList<tStatValue> ListValue => m_listValue;
}
