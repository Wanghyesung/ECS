using R3;
using UnityEngine;

/*///////////////////////////////////////////
                Shop
목적 : 상점의 장비 선택·구매 UI를 처리하고 획득한 장비를 PlayerInventory에 전달한다.
 *///////////////////////////////////////////

public class Shop : MonoBehaviour
{
    [SerializeField] private Container m_refShopContainer;
    [SerializeField] private Container m_refInventoryContainer;

    [SerializeField] private DataDescUI m_refSelectDescUI;

    [SerializeField] private BaseButtonUI m_refBuyButton;
    private SOData m_refSelectData;
    private DisposableBag m_bagEvents;

    private void OnEnable()
    {
        m_refShopContainer.Init();
        m_refInventoryContainer.Init();
        ProfileSave.m_Instance.Inventory.RestoreInventory(m_refInventoryContainer);

        m_refShopContainer.OnSelectEvt.Subscribe(ShowItem).AddTo(ref m_bagEvents);
        m_refBuyButton.OnClickEvt.Subscribe(_ => BuyItem()).AddTo(ref m_bagEvents);
    }

    private void OnDisable()
    {
        m_bagEvents.Clear();
    }


    private void ShowItem(SOData _refData)
    {
        m_refSelectData = _refData;
        m_refSelectDescUI.Show(_refData);
    }

    private void BuyItem()
    {
        if (m_refSelectData is not SOEqipData refItem)
            return;

        ProfileSave refProfile = ProfileSave.m_Instance;
        if (refProfile.Inventory.CanRegisterItem(refItem) == false)
            return;
        if (refProfile.Currency.Amount.CurrentValue < refItem.Price)
            return;
        if (m_refInventoryContainer.AddData(refItem) == false)
            return;

        if (refProfile.Currency.TrySpend(refItem.Price, false) == false)
        {
            m_refInventoryContainer.DeleteData(refItem);
            return;
        }
        if (refProfile.Inventory.AcquireItem(refItem) == false)
        {
            m_refInventoryContainer.DeleteData(refItem);
            refProfile.Currency.Add(refItem.Price);
            return;
        }

        refProfile.Save(refProfile.Currency);
        m_refSelectDescUI.Show(null);
        m_refSelectData = null;
    }
}
