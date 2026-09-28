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
    Weapon = 0,
    Gloves = 1,
    Helmet = 2,
    Shoes = 3,
    End = 10,
}

[Serializable]
public struct tStatValue
{
    public eStatType Type;
    public float Value;
}

/*///////////////////////////////////////////
                SOEqipData
목적 : 장비의 설명·아이콘·고유 저장 ID·스탯을 담는 ScriptableObject 데이터.
      PlayerInventory가 ID로 장비를 복원하고 PlayerPreLoadData가 장착 스탯을 적용한다.
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_EquipData", menuName = "Game/Item/SOEquipData")]
public class SOEqipData : SOData
{
    public override eDataType DataType => eDataType.Equip;

    [SerializeField] private string m_strSaveId;
    public string SaveId => m_strSaveId;

    [SerializeField] private eEquipType m_eEquipType = eEquipType.End;
    public override int SubDataType => (int)m_eEquipType;
    public eEquipType EquipType => m_eEquipType;

    [SerializeField] private List<tStatValue> m_listValue = new List<tStatValue>();
    public IReadOnlyList<tStatValue> ListValue => m_listValue;

}
