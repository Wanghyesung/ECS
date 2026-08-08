using DG.Tweening;
using System.Reflection;
using UnityEngine;

/*///////////////////////////////////////////
                EquipController
기능 : 아이템(SOEqipData) 보관/적용을 담당하는 매니저. 로비에서만 조작이 일어나지만
      스테이지에서 얻은 아이템을 역추적 없이 바로 반영할 수 있도록 씬 전환에 걸쳐 유지된다.
      인벤토리(보관) Container / 장착(적용) Container를 각각 직렬화로 받아
      OnSelectEvt를 구독해 보관/적용 로직을 처리한다
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
        if (m_Instance != null)
            Destroy(this);

        m_Instance = this;
        DontDestroyOnLoad(this);
    }

    private void Start()
    {
        m_refInventoryContainer.OnSelectSlotView += PickInventorySlot;
        m_refPlusButton.OnClickEvt += PushInterface;
        m_refEquipInterface.OnAddData += PushAndApply;

        //m_refInventoryContainer.OnSelectEvt += AddData;
        //m_refInterFaceContainer.OnSelectEvt += PushAndApply;
    }

    private void OnDestroy()
    {
        m_refInventoryContainer.OnSelectSlotView -= PickInventorySlot;
        m_refPlusButton.OnClickEvt -= PushInterface;
        m_refEquipInterface.OnAddData -= PushAndApply;

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

    private void PushAndApply(SOData _refSOData)
    {
        SOEqipData refItemData = _refSOData as SOEqipData;
        if (refItemData == null)
            return;

        PlayerPreLoadData.AddStatRange(refItemData.ListValue);
    }

    private void PushInterface()
    {
        m_refPlusButton.gameObject.SetActive(false);

        //기존에 잡은 데이터 원본 컨테이너에서 지우고 인터페이스에 저장
        m_refInventoryContainer.DeleteData(m_refInventoryPick);
        SOData SOPreData = m_refEquipInterface.AddSwapData(m_refInventoryPick);
        if(SOPreData != null)
            m_refInventoryContainer.AddData(SOPreData);
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
