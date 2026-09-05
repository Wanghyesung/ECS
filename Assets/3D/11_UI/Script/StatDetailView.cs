using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*///////////////////////////////////////////
                StatDetailView
목적 : 강화창에서 현재 선택된 스탯 하나(아이콘/이름/수치/레벨)를 그리기만 하는 순수 View.
      값을 어떻게 계산하는지는 모르고, Presenter(PlayerStatUI)가 계산해서 넘겨주는 값을
      그대로 표시만 한다.
 *///////////////////////////////////////////
public sealed class StatDetailView : MonoBehaviour
{
    [SerializeField] private Image m_refIcon;
    [SerializeField] private TextMeshProUGUI m_refNameText;
    [SerializeField] private TextMeshProUGUI m_refValueText;
    [SerializeField] private TextMeshProUGUI m_refLevelText;

    public void Show(Sprite _refIcon, string _strName, string _strValue, int _iLevel)
    {
        m_refIcon.sprite = _refIcon;
        m_refNameText.text = _strName;
        m_refValueText.text = _strValue;
        m_refLevelText.text = "Lv." + _iLevel;
    }
}
