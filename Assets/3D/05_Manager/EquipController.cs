using DG.Tweening;
using R3;
using UnityEngine;

/*///////////////////////////////////////////
                EquipController
목적 : 로비의 보유·장착 UI를 PlayerInventory 상태로 복원하고 장착 입력을 처리한다.
      장착이 확정되면 PlayerInventory에 변경을 전달해 프로필 저장으로 이어준다.
 *///////////////////////////////////////////

public class EquipController : MonoBehaviour
{
    private struct tPickData
    {
        public SOData TargetData;
    }

    public static EquipController m_Instance = null;

    [SerializeField] private Container m_refInventoryContainer; // 보관
    [SerializeField] private Interface m_refEquipInterface;
    private SOEqipData m_refInventoryPick = null;

    //인벤토리에서 지정한 데이터를 인터페이스에 올릴지 최종 체크하는 버튼
    [SerializeField] private BaseButtonUI m_refPlusButton;
    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(this);
    }

    private void Start()
    {
        m_refInventoryContainer.Init();
        ProfileSave.m_Instance.Inventory.RestoreInventory(m_refInventoryContainer);
        ProfileSave.m_Instance.Inventory.RestoreEquipment(m_refEquipInterface);

        m_refInventoryContainer.OnSelectSlotView.Subscribe(PickInventorySlot).AddTo(this);
        m_refPlusButton.OnClickEvt.Subscribe(_ => PushInterface()).AddTo(this);
    }

    private void OnEnable()
    {
        m_refInventoryPick = null;
        m_refPlusButton.gameObject.SetActive(false);
    }

    private void PushEventory(SOData _refSOData, int _iCount = 1)
    {
        m_refInventoryContainer.AddData(_refSOData, _iCount);
    }

    private void PushInterface()
    {
        m_refPlusButton.gameObject.SetActive(false);

        if (m_refInventoryPick == null || ProfileSave.m_Instance.Inventory.CanEquipItem(m_refInventoryPick) == false)
            return;
        if (m_refEquipInterface.FindDataIdx(m_refInventoryPick) < 0)
            return;

        //기존에 잡은 데이터 원본 컨테이너에서 지우고 인터페이스에 저장
        if (m_refInventoryContainer.DeleteData(m_refInventoryPick) == false)
            return;

        SOEqipData refPrevious = m_refEquipInterface.AddSwapData(m_refInventoryPick) as SOEqipData;
        if (refPrevious != null)
            m_refInventoryContainer.AddData(refPrevious);

        if (ProfileSave.m_Instance.Inventory.EquipItem(m_refInventoryPick, refPrevious) == false)
            Debug.LogError("장비 교체를 프로필에 반영하지 못했습니다.");
        m_refInventoryPick = null;
    }

    //private void SelectInterfaceView(SlotView _refClickView)
    //{
    //    _refClickView.PushScale();
    //
    //    RectTransform refRectTr = (RectTransform)_refClickView.transform;
    //    Vector2 vAnchorPos = refRectTr.anchoredPosition;
    //
    //    m_refPlusButton.gameObject.SetActive(true);
    //    RectTransform refButtonTr = (RectTransform)m_refPlusButton.transform;
    //    refButtonTr.position = vAnchorPos;
    //}

    private void PickInventorySlot(SlotView _refSlotView)
    {
        m_refInventoryPick = _refSlotView.SOData as SOEqipData;

        Vector3 vViewPosition =
            m_refEquipInterface.FindSlotPosition(m_refInventoryPick.DataType, m_refInventoryPick.SubDataType);

        if (vViewPosition == Vector3.zero)
            return;

        m_refPlusButton.gameObject.SetActive(true);
        m_refPlusButton.transform.position = vViewPosition;
    }

}
