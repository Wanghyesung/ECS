using R3;
using TMPro;
using UnityEngine;

/*///////////////////////////////////////////
                CurrencyHudView
목적 : 로비 상단에 항상 떠있는 재화(골드) 표시. ProfileSave.Currency.Amount 를
      구독해서 강화창 등 어디서 재화가 변하든 실시간으로 반영한다.
 *///////////////////////////////////////////
public class CurrencyHudView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI m_refAmountText;

    private void Awake()
    {
        ProfileSave.m_Instance.Currency.Amount.Subscribe(_iAmount => m_refAmountText.text = _iAmount.ToString()).AddTo(this);
    }
}
