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
    public class SlotInfo
    {
        public Vector2 vSlotSize;
        public Vector2 vPosition;
        public eDataType eType;
        public int iSubType;

        public SlotView refSlotView;
    }

    [SerializeField] private List<SlotInfo> m_listView = new List<SlotInfo>();

    private SlotView m_refTargetSlot;

    //콜백함수
    public Action<SOData> OnAddData;
    public Action<SOData> OnSelectEvt;
    public Action<SlotView> OnSelectSlotView;
      

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

            if (pSlotInfo.refSlotView.SOData != null)
                return false; //이미 장착된 소켓 (교체는 별도 처리 필요)

            pSlotInfo.refSlotView.Bind(_SOData, i);
            OnAddData?.Invoke(_SOData);
            return true;
        }

        return false;
    }

    //지정된 위치에 이미 데이터가 있다면 반환해주고 데이터 넣기
    public SOData AddSwapData(SOData _SOData)
    {
        int iFindIdx = FindDataIdx(_SOData);
        SOData SOPreData = null;

        if (iFindIdx != -1)
        {
            SOPreData = m_listView[iFindIdx].refSlotView.SOData;
            DeleteData(iFindIdx);
        }
        AddData(_SOData);

        return SOPreData;
    }

    public bool DeleteData(SOData _SOData)
    {
        for (int i = 0; i < m_listView.Count; ++i)
        {
            var pSlotInfo = m_listView[i];
            if (pSlotInfo.refSlotView.SOData != _SOData)
                continue;

            pSlotInfo.refSlotView.Bind(null, i);
            return true;
        }

        return false;
    }

    public bool DeleteData(int _iDataIdx)
    {
        if(m_listView.Count <= _iDataIdx || 0>m_listView.Count)
            return false;

        m_listView[_iDataIdx].refSlotView.Bind(null, _iDataIdx);
        return true;
    }

    public int FindDataIdx(SOData _SOData)
    {
        for (int i = 0; i < m_listView.Count; ++i)
        {
            SlotInfo refSlotInfo = m_listView[i];
            if (refSlotInfo.eType == _SOData.DataType && refSlotInfo.iSubType == _SOData.SubDataType)
                return i;
        }
        return -1;
    }
    
    public Vector3 FindSlotPosition(eDataType _eDataType, int _iSubDataType)
    {
        for (int i = 0; i < m_listView.Count; ++i)
        {
            if (m_listView[i].eType == _eDataType && m_listView[i].iSubType == _iSubDataType)
                return m_listView[i].refSlotView.transform.position;
        }
        return Vector3.zero;
    }

    public int FindDataIdx(eDataType _eDataType, int _iSubDataType)
    {
        for (int i = 0; i < m_listView.Count; ++i)
        {
            if (m_listView[i].eType == _eDataType && m_listView[i].iSubType == _iSubDataType)
                return i;
        }

        return -1;
    }

    public void SetTargetSlot(SlotView _pTargetSlot)
    {
        m_refTargetSlot = _pTargetSlot;

        //콜백함수
        OnSelectEvt?.Invoke(_pTargetSlot.SOData);
        OnSelectSlotView?.Invoke(_pTargetSlot);
    }


}
