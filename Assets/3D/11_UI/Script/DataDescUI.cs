using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 기존 설명 UI와 선택적으로 연결한 가격 UI에 데이터만 표시한다.
public class DataDescUI : MonoBehaviour
{
    [SerializeField] private Sprite m_refOriginSprite;
    [SerializeField] private Image m_refImage;
    [SerializeField] private TextMeshProUGUI m_refDescTex;
    [SerializeField] private GameObject m_refPriceRow;
    [SerializeField] private TextMeshProUGUI m_refPriceTex;

    private void Awake()
    {
        if (m_refPriceRow != null)
            m_refPriceRow.SetActive(false);
    }

    public void Show(SOData _refData)
    {
        if (_refData == null)
        {
            m_refImage.sprite = m_refOriginSprite;
            m_refDescTex.text = "";
        }
        else
        {
            m_refImage.sprite = _refData.Icon;
            m_refDescTex.text = _refData.Description;
        }

        // 공용 툴팁처럼 가격 UI를 배치하지 않은 뷰는 설명만 표시한다.
        if (m_refPriceRow == null)
            return;

        if (_refData is SOEqipData refEquipment)
        {
            m_refPriceTex.SetText("{0}", refEquipment.Price);
            m_refPriceRow.SetActive(true);
        }
        else
            m_refPriceRow.SetActive(false);
    }
}
