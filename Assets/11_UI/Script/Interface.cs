using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif


public interface ISelectDataable
{
    void SetTargetSlot(SlotView _pTargetSlot);
    void OnBeginDrag(PointerEventData e);
    void OnDrag(PointerEventData e);
    void OnEndDrag(PointerEventData e);
}


public class Interface : BaseButtonUI, ISelectDataable
{
    [Serializable]
    private class tSlotInfo
    {
        public Vector2 vSlotSize;
        public Vector2 vPosition;
        public eDataType eType;
        public int iSubType;

        public SlotView refSlotView;
    }

    [SerializeField] private List<tSlotInfo> m_listView = new List<tSlotInfo>();

    private SlotView m_refTargetSlot;

    public Action<SOData> OnSelectEvt;
    public Action<SlotView> OnSelectSlotView;
        //콜백함수
      

    [Header("BUILD")]
    [SerializeField] private SlotView m_refSlotPrefab;
    [SerializeField] private RectTransform m_refContentView; // 슬롯들이 붙는 부모 Rect

    //빌드 전용
    public bool Run = false;

    protected void Awake()
    {
        Build();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 에디터 미리보기 전용
        if (Application.isPlaying || Run)
            return;

        //재 진입 방지 , 예외가 나면 Run이 false가 안될 수 있으므로 finally에서 false로
        Run = true;
        EditorApplication.delayCall += () =>
        {
            try
            {
                if (this == null)
                    return;

                Build();
            }
            finally
            {
                Run = false;
            }
        };
    }
#endif

    //tSlotInfo에 미리 지정된 위치(vPosition) 기준으로 슬롯 생성 (Container와 달리 그리드 자동배치 없음)
    public void Build()
    {
        ClearData();

        for (int i = 0; i < m_listView.Count; ++i)
        {
            SlotView pSlot = Instantiate(m_refSlotPrefab, m_refContentView);
            pSlot.Init(this);

            var pRect = (RectTransform)pSlot.transform;
            pRect.anchoredPosition = m_listView[i].vPosition;
            pRect.sizeDelta = m_listView[i].vSlotSize;

            m_listView[i].refSlotView = pSlot;
        }
    }

    private void ClearData()
    {
        //기존 리스트 삭제 (에디터 버전 오브젝트 삭제)
        for (int i = m_refContentView.childCount - 1; i >= 0; --i)
        {
            if (m_refContentView.GetChild(i).gameObject.GetComponent<SlotView>())
            {
#if UNITY_EDITOR
                Undo.DestroyObjectImmediate(m_refContentView.GetChild(i).gameObject);
#else
                Destroy(m_refContentView.GetChild(i).gameObject);
#endif
            }
        }

        for (int i = 0; i < m_listView.Count; ++i)
            m_listView[i].refSlotView = null;
    }

    //해당 SOData의 (DataType, SubDataType)에 맞는 소켓을 찾아 장착
    public bool AddData(SOData _SOData)
    {
        for (int i = 0; i < m_listView.Count; ++i)
        {
            var pSlotInfo = m_listView[i];
            if (pSlotInfo.eType != _SOData.DataType || pSlotInfo.iSubType != _SOData.SubDataType)
                continue;

            if (pSlotInfo.refSlotView.SOFeat != null)
                return false; //이미 장착된 소켓 (교체는 별도 처리 필요)

            pSlotInfo.refSlotView.Bind(_SOData, i);
            return true;
        }

        return false;
    }

    public bool DeleteData(SOData _SOData)
    {
        for (int i = 0; i < m_listView.Count; ++i)
        {
            var pSlotInfo = m_listView[i];
            if (pSlotInfo.refSlotView.SOFeat != _SOData)
                continue;

            pSlotInfo.refSlotView.Bind(null, i);
            return true;
        }

        return false;
    }

    public int FindDataIdx(SOData _SOData)
    {
        for (int i = 0; i < m_listView.Count; ++i)
        {
            if (m_listView[i].refSlotView.SOFeat == _SOData)
                return i;
        }

        return -1;
    }

    public void SetTargetSlot(SlotView _pTargetSlot)
    {
        m_refTargetSlot = _pTargetSlot;

        //콜백함수
        OnSelectEvt?.Invoke(_pTargetSlot.SOFeat);
        OnSelectSlotView?.Invoke(_pTargetSlot);
    }


}
