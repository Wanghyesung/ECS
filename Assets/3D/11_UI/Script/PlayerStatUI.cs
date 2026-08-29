using UnityEngine;

/*///////////////////////////////////////////
                PlayerStatUI
목적 : 로비 옵션 탭에서 여는 플레이어 능력치 강화창의 Presenter(오케스트레이터).
      DungeonManager가 스폰을 ObjectSpawner에 위임하듯, 실제 표시는
      StatDetailView/UpgradeButtonView 두 View 컴포넌트에 위임하고
      이 클래스는 "무엇을 언제 보여줄지"(선택/계산/트랜잭션)만 담당한다.
      강화 버튼을 누르면 PlayerCurrency에서 재화를 차감한 뒤 장비 스탯과 동일하게
      PlayerPreLoadData에 보너스를 추가한다(파이프라인 재사용).
      레벨은 따로 저장하지 않고 PlayerPreLoadData의 누적 보너스에서 역산한다
      (씬이 다시 로드돼도 실제 적용된 보너스와 항상 일치하도록).
 *///////////////////////////////////////////
public sealed class PlayerStatUI : MonoBehaviour, ICountable
{

    [SerializeField] private SOObjectInfo m_refBaseInfo;
    [SerializeField] private Container m_refContainer;

    [Header("View")]
    [SerializeField] private StatDetailView m_refDetailView;
    [SerializeField] private UpgradeButtonView m_refUpgradeView;

    private SOStatUpgradeData m_refSelectData;

 
    private void Start()
    {
        m_refContainer.Init();
        m_refContainer.OnSelectEvt += SelectStat;
        m_refUpgradeView.OnClickEvt += TryUpgrade;

    }

    private void OnDestroy()
    {
     

        m_refContainer.OnSelectEvt -= SelectStat;
        m_refUpgradeView.OnClickEvt -= TryUpgrade;
    }

    private void OnEnable()
    {
        if (m_refSelectData == null)
        {
            SOData refFirst = m_refContainer.GetDataIdx(0, eDataType.StatUpgrade);
            if (refFirst != null)
                SelectStat(refFirst);
        }
        else
        {
            RefreshSelected();
        }
    }

    private void SelectStat(SOData _refData)
    {
        if (_refData is not SOStatUpgradeData refStatData)
            return;

        m_refSelectData = refStatData;
        RefreshSelected();
    }

    private void TryUpgrade()
    {
        if (m_refSelectData == null)
            return;

        int iCost = GetCost(m_refSelectData);
        if (PlayerCurrency.TrySpend(iCost) == false)
            return;

        PlayerPreLoadData.AddStat(m_refSelectData.AddValue.Type, m_refSelectData.AddValue.Value);

        RefreshSelected();
        m_refContainer.BindData(m_refContainer.CurrentCategoryIdx);
    }

    private void RefreshSelected()
    {
        if (m_refSelectData == null)
            return;

        int iLevel = GetLevel(m_refSelectData);
        int iCost = GetCost(m_refSelectData);

        m_refDetailView.Show(m_refSelectData.Icon, m_refSelectData.DisplayName, GetDisplayValue(m_refSelectData), iLevel);
        m_refUpgradeView.Show(iCost, PlayerCurrency.Amount >= iCost);
    }

    // HP는 기본값+보너스 합계로, 그 외 스탯은 진짜 기본값이 무기/이동 등에 흩어져
    // 있어 로비에서 알 수 없으므로 장비/강화로 얻은 보너스 합계만 표시한다.
    private string GetDisplayValue(SOStatUpgradeData _refData)
    {
        float fBonus = PlayerPreLoadData.GetPendingTotal(_refData.AddValue.Type);
        if (_refData.AddValue.Type == eStatType.HP)
            return ((long)(m_refBaseInfo.MaxHP + fBonus)).ToString();

        return "+" + fBonus.ToString("0.#");
    }

    private int GetLevel(SOStatUpgradeData _refData)
    {
        return Mathf.RoundToInt(PlayerPreLoadData.GetPendingTotal(_refData.AddValue.Type) / _refData.AddValue.Value);
    }

    private int GetCost(SOStatUpgradeData _refData)
    {
        return _refData.BaseCost + GetLevel(_refData) * _refData.CostIncrement;
    }

    // ICountable: Container가 슬롯 뱃지에 표시할 레벨을 조회할 때 사용
    public int GetCount(SOData _refSO)
    {
        if (_refSO is not SOStatUpgradeData refData)
            return 0;

        return GetLevel(refData);
    }
}
