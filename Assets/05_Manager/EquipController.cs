using DG.Tweening;
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
        m_refInventoryContainer.OnSelectEvt += PickInventoryItem;
        

        //m_refPlusButton.OnClickEvt += PushInterface;

        //m_refInventoryContainer.OnSelectEvt += AddData;
        //m_refInterFaceContainer.OnSelectEvt += PushAndApply;
    }

    private void OnDestroy()
    {
        m_refInventoryContainer.OnSelectEvt -= PickInventoryItem;
       

        m_refPlusButton.OnClickEvt -= PushInterface;
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

        Player refPlayer = Player.CurrentPlayer;
        var listValue = refItemData.ListValue;

        for (int i = 0; i < listValue.Count; ++i)
            ApplyItemData(refPlayer, listValue[i]);
    }

    private void PushInterface()
    {
        if (m_refInventoryPick == null)
            return;
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

    private void ApplyItemData(Player _refPlayer, tStatValue _tModifier)
    {
        switch (_tModifier.Type)
        {
            case eStatType.HP:
                _refPlayer.AddHP((long)_tModifier.Value);
                break;
            case eStatType.Attack:
                _refPlayer.AddAttack((int)_tModifier.Value);
                break;
            case eStatType.Defense:
                _refPlayer.AddDefense(_tModifier.Value);
                break;
            case eStatType.Speed:
                _refPlayer.AddSpeed(_tModifier.Value);
                break;
            case eStatType.BulletSpeed:
                _refPlayer.UpBulletSpeed(_tModifier.Value);
                break;
        }
    }

    private void PickInventoryItem(SOData _SOData)
    {
        m_refInventoryPick = _SOData as SOEqipData;

    
    }

}
