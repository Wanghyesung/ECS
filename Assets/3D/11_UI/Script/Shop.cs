using R3;
using UnityEngine;

/*///////////////////////////////////////////
                Shop
목적 : 상점의 장비 선택·획득 UI를 처리하고 획득한 장비를 PlayerInventory에 전달한다.
      현재는 가격 차감 기능이 없어 장비 획득과 프로필 저장만 처리한다.
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

    //TODO : 재화에 맞게 
    private void BuyItem()
    {
        if (m_refSelectData is not SOEqipData refItem)
            return;

        if (ProfileSave.m_Instance.Inventory.CanRegisterItem(refItem) == false)
            return;
        if (m_refInventoryContainer.AddData(refItem) == false)
            return;

        if (ProfileSave.m_Instance.Inventory.AcquireItem(refItem) == false)
        {
            m_refInventoryContainer.DeleteData(refItem);
            return;
        }
        m_refSelectDescUI.Show(null);
        //SOData as SO
    }
}
