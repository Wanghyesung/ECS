using TMPro;
using UnityEngine;

/*///////////////////////////////////////////
                CurrencyHudView
목적 : 로비 상단에 항상 떠있는 재화(골드) 표시. PlayerCurrency.OnAmountChanged를
      구독해서 강화창 등 어디서 재화가 변하든 실시간으로 반영한다.
 *///////////////////////////////////////////
public class CurrencyHudView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI m_refAmountText;

    private void OnEnable()
    {
        PlayerCurrency.OnAmountChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerCurrency.OnAmountChanged -= Refresh;
    }

    private void Refresh()
    {
        m_refAmountText.text = PlayerCurrency.Amount.ToString();
    }
}
