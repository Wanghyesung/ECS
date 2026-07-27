using UnityEngine;

/*///////////////////////////////////////////
                ItemManager
기능 : 아이템(SOItemData) 보관/적용을 담당하는 매니저. 로비에서만 조작이 일어나지만
      스테이지에서 얻은 아이템을 역추적 없이 바로 반영할 수 있도록 씬 전환에 걸쳐 유지된다.
      인벤토리(보관) Container / 장착(적용) Container를 각각 직렬화로 받아
      OnSelectEvt를 구독해 보관/적용 로직을 처리한다
 *///////////////////////////////////////////

public class ItemManager : MonoBehaviour
{
    public static ItemManager m_Instance = null;

    [SerializeField] private Container m_refInventoryContainer; // 보관
    [SerializeField] private Container m_refInterFaceContainer; // 장착/적용

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(this);

        m_Instance = this;
        DontDestroyOnLoad(this);
    }

    private void Start()
    {
        //m_refInventoryContainer.OnSelectEvt += AddData;
        //m_refInterFaceContainer.OnSelectEvt += PushAndApply;
    }


    private void PushEventory(SOData _refSOData, int _iCount = 1)
    {
        m_refInventoryContainer.AddData(_refSOData, _iCount);
    }

    private void PushInterface(SOData _refSOData, int _iCount = 1)
    {
        m_refInterFaceContainer.AddData(_refSOData, _iCount);
    }

    private void PushAndApply(SOData _refSOData)
    {
        SOItemData refItemData = _refSOData as SOItemData;
        if (refItemData == null)
            return;

        Player refPlayer = Player.CurrentPlayer;
        var listValue = refItemData.ListValue;

        for (int i = 0; i < listValue.Count; ++i)
            ApplyItemData(refPlayer, listValue[i]);
    }

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
}
