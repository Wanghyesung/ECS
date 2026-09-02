using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DataDescUI : MonoBehaviour
{
    [SerializeField] private Sprite m_refOriginSprite;
    [SerializeField] private Image m_refImage;
    [SerializeField] private TextMeshProUGUI m_refDescTex;

    public void Show(SOData _refData)
    {
        if(_refData == null)
        {
            m_refImage.sprite = m_refOriginSprite;
            m_refDescTex.text = "";
        }
        else
        {
            m_refImage.sprite = _refData.Icon;
            m_refDescTex.text = _refData.Description;
        }
    }

}
