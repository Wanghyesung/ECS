using UnityEngine;

/*///////////////////////////////////////////
                SOStatUpgradeData
목적 : 로비 능력치 강화창(Container)이 보여주는 스탯 하나의 정적 데이터.
      실제 레벨/누적 보너스는 SO가 아닌 PlayerPreLoadData(정적 버퍼)가 들고 있어서
      SO 데이터 오염 없이 여러 세션에서 같은 에셋을 공유해도 안전하다.
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_StatUpgrade", menuName = "Game/Stat/SOStatUpgradeData")]
public class SOStatUpgradeData : SOData
{
    public override eDataType DataType => eDataType.StatUpgrade;

    [SerializeField] private string m_strDisplayName;
    public string DisplayName => m_strDisplayName;

    [Header("강화 밸런스")]
    // 강화 1회당 플레이어에게 더해지는 스탯. PlayerPreLoadData.AddStat()으로 그대로 전달되어
    // 게임 로딩 시 Base(SOObjectInfo) + Weapon(무기 자체 스탯) + AddValue 순으로 합산 적용된다.
    [SerializeField] private tStatValue m_tAddValue;
    public tStatValue AddValue => m_tAddValue;
    public override int SubDataType => (int)m_tAddValue.Type;

    [SerializeField] private int m_iBaseCost = 50;
    public int BaseCost => m_iBaseCost;

    [SerializeField] private int m_iCostIncrement = 25;
    public int CostIncrement => m_iCostIncrement;
}
