using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shop : MonoBehaviour
{
    [SerializeField] private Container m_refShopContainer;
    [SerializeField] private Container m_refInventoryContainer;

    [SerializeField] private DataDescUI m_refSelectDescUI;

    [SerializeField] private BaseButtonUI m_refBuyButton;
    private SOData m_refSelectData;
    private void OnEnable()
    {
        m_refShopContainer.Init();
        m_refInventoryContainer.Init();

        m_refShopContainer.OnSelectEvt += ShowItem;
        m_refBuyButton.OnClickEvt += BuyItem;
    }


    private void ShowItem(SOData _refData)
    {
        m_refSelectData = _refData;
        m_refSelectDescUI.Show(_refData);
    }

    //TODO : 재화에 맞게 
    private void BuyItem()
    {
        if (m_refSelectData == null)
            return;

        m_refInventoryContainer.AddData(m_refSelectData);
        m_refSelectDescUI.Show(null);
        //SOData as SO
    }
}
