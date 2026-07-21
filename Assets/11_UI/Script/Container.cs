using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[Serializable]
public class CategoryData
{
    public List<SOData> ListData = new List<SOData>();

    [HideInInspector] public int m_iCurrentRemnantData = 0;

    [SerializeField] private bool m_bCanDuplication = true; //중복 허용할지(장비 템 창, 스킬 창)
    public bool IsCanDuplication => m_bCanDuplication;
    public bool IsFull => m_iCurrentRemnantData <= 0;
    [HideInInspector] public int m_iCategoryIdx = 0;

    //이 카테고리가 보여줄 데이터의 카운트를 어디서 가져올지 (예: FeatureManager). 카테고리마다 다른 출처를 꽂을 수 있음
    [SerializeField] private MonoBehaviour m_refCountSourceObj;
    public ICountable CountSource => m_refCountSourceObj as ICountable;

    //[SerializeField] public eUIType m_iUIType = eUIType.None;
    public int GetRemnantDataIdx()
    {
        for (int i = 0; i < ListData.Count; ++i)
        {
            if (ListData[i] == null)
                return i;
        }

        return -1;
    }
}

[Serializable]
public enum eContainerType
{
    None,
    Feature,
}

/*//////////////////////////////////////////////
기능 : 특정 SO를 보관하고 관리해주는 역할, 동적으로 컨테이너 크기를 키워주고 
      사용자와 상호작용(드래그 클릭) 역할을 수행
 *//////////////////////////////////////////////

public class Container : BaseButtonUI
{
    //UI 컨테이너 (스킬창, 인벤토리 창)

    //view담당 (고정된 슬롯을 렌더링하게 슬롯이 100개면 보이는구간만 렌더링되게)
    //data 담당 (SO를 활용해서 초기 데이터 저장)
    //controll은 UGUI pointer에서 담당
    //카테고리별로 슬롯뷰는 동일하되 데이터는 따로 보여줄 수 있게 

    [Header("CONTANIER")]
    private RectMask2D m_refRectMask;

    [SerializeField] eContainerType m_eType; //어떤 도메인 데이터를 다루는 컨테이너인지 (Feature면 FeatureManager와 연동)

    [SerializeField] private RectTransform m_refContainerView; // 프레임(마스크) Rect
    [SerializeField] private RectTransform m_refContentView;   // 셀들이 붙는 부모 Rect

    //[SerializeField] private List<SOEntryUI> m_listData = new List<SOEntryUI>(); //실세 데이터
    [SerializeField] private List<CategoryData> m_listCategoryData = new List<CategoryData>();
    private List<SlotView> m_listView = new List<SlotView>();
    [SerializeField] private int m_iCurrentCategoryIdx = 0;
    public int CurrentCategoryIdx { get => m_iCurrentCategoryIdx; }


    [SerializeField] private int m_iCategoryCount = 0;
    public int CategoryCount { get => m_iCategoryCount; }

    //[SerializeField] private bool m_bOnSelect = false;
    //public bool IsOnSelect { get => m_bOnSelect; }

    [SerializeField] private SlotView m_refSlotPrefab; //셀 프리팹
    [SerializeField] private Vector2 m_vStep;     //셀 사이 간격(x=열 간, y=행 간)
    [SerializeField] private Vector2 m_vPadding;  // L,T,R,B (컨테이너 내부 여백)

    [Header("TargetSLOT")]
    private SlotView m_refTargetSlot;//id로 바꿀 수 있음 (매니저에서 가져오게 아니면 그냥 SO들고있기)

    public event Action<SOData> OnSelectEvt;
    public event Action<SOData> OnAddEvt;
    public event Action<SOData> OnDeleteEvt;
    public event Action<SOData> OnFullEvt;

    [SerializeField] private GameObject m_refSelectFramePrefab;
    private RectTransform m_refFrameRectTrasnform;
    private Image m_refFrameImage;

    [Header("SLOT")]
    [SerializeField] private int m_iSlotColCount = 3;
    [SerializeField] private int m_iSlotRowCount = 2;

    [SerializeField] private Vector2 m_vSlotSize = Vector2.zero;

    //부드러운 이동을 위해 버퍼를 +1씩더 만들기
    private int m_iColCount = -1;
    private int m_iRowCount = -1;

    //현재 바라보고있는 행 위치
    private int m_iCurRow = 0;

    [Header("DRAG")]
    private Vector2 m_vDragCurPosition = Vector2.zero; //저번 프레임과 이번 프레임의 차이를 구하기 위해

    private Vector2 m_vContainerDragPosition = Vector2.zero;
    private Vector2 m_vViewCurDrageLine = Vector2.zero;//현재 드래그 라인
    private Vector2 m_vContaninerSize = Vector2.zero; //전체 컨테이너 크기
    private Vector2 m_vViewOriginPos = Vector2.zero;//현재 드래그 라인


    //빌드 전용
    public bool Run = false;

    protected void Awake()
    {
        m_vViewOriginPos = m_refContentView.anchoredPosition;

        Build();
    }

    public void Init()
    {
        //Feature 전용 컨테이너면 기능 흭득/레벨업 이벤트를 구독해서 슬롯에 자동 반영
        if (m_eType == eContainerType.Feature && FeatureManager.m_Instance != null)
            FeatureManager.m_Instance.OnFeatureSelect += OnFeatureAcquired;

        if (m_refSelectFramePrefab != null)
        {
            GameObject pFrameObejct = Instantiate(m_refSelectFramePrefab, m_refContentView);
            m_refFrameImage = pFrameObejct?.GetComponent<Image>();
            m_refFrameImage.enabled = false;

            m_refFrameRectTrasnform = pFrameObejct?.GetComponent<RectTransform>();
        }
    }

    //FeatureManager.OnFeatureSelect(Action<SOFeature,int>) 시그니처에 맞춘 전용 래퍼
    private void OnFeatureAcquired(SOFeature _refFeature, int _iNewLevel)
    {
        AddData(_refFeature, _iNewLevel);
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

    /*/////////////////////////////////////
                    Conatiner
     *////////////////////////////////////

    //빈공간 없이 정렬
    public void SortData()
    {
        for (int i = 1; i < m_listView.Count; ++i)
        {
            SlotView refTarget = m_listView[i];
            if (refTarget.SOFeat == null)
                continue;

            int pre = i - 1;
            while (pre >= 0 && m_listView[pre].SOFeat != null)
                --pre;

            //앞쪽에 빈 슬롯이 없으면 옮길 곳이 없으므로 스킵
            if (pre < 0)
                continue;

            int iPreCount = m_listView[pre].Count;
            m_listView[pre].Bind(refTarget.SOFeat, pre, iPreCount);
            m_listView[i].Bind(null, i);
        }
    }

    public bool DeleteData(int _iDataIdx, int _iCategoryIdx = 0)
    {
        CategoryData refCategoryData = GetCategoryData(_iCategoryIdx);
        if (refCategoryData == null || refCategoryData.ListData[_iDataIdx] == null)
            return false;

        var SOData = refCategoryData.ListData[_iDataIdx];

        refCategoryData.ListData[_iDataIdx] = null;
        ++refCategoryData.m_iCurrentRemnantData;
    
        //데이터 새로 바인딩
        BindData(_iCategoryIdx);
        OnDeleteEvt?.Invoke(SOData);

        return true;
    }

    public bool DeleteData(SOData _SOData, int _iCategoryIdx = 0)
    {
        CategoryData refCategoryData = GetCategoryData(_iCategoryIdx);
        if (refCategoryData == null)
            return false;

       
        var listData = refCategoryData.ListData;
        bool bFind = false; 
        for(int i = 0; i<listData.Count; ++i)
        {
            if (listData[i] == _SOData)
            {
                refCategoryData.ListData[i] = null;
                ++refCategoryData.m_iCurrentRemnantData;
                bFind = true;
            }
        }

        if (bFind == false)
            return false;

        SortData();
        OnDeleteEvt?.Invoke(_SOData);
        return true;
    }


    //FeatureManager.OnFeatureSelect 핸들러 전용: 처음 흭득이면 목록에 새로 추가하고,
    //이미 보유 중인 기능이면(레벨업) 추가 없이 카운트(레벨)만 갱신
    public bool AddData(SOData _SOData, int _iCount, int _iCategoryIdx = 0)
    {
        CategoryData pCategoryData = GetCategoryData(_iCategoryIdx);
        if (pCategoryData == null)
            return false;

        //이미 보유 중인 기능인지(=레벨업) 확인
        int iIdx = pCategoryData.ListData.IndexOf(_SOData);

        if (iIdx == -1)
        {
            //처음 흭득 -> 남는 자리에 새로 추가
            if (pCategoryData.IsFull == true)
                return false;

            iIdx = pCategoryData.GetRemnantDataIdx();
            if (iIdx == -1)
                return false;

            pCategoryData.ListData[iIdx] = _SOData;
            --pCategoryData.m_iCurrentRemnantData;


            //구조가 바뀌었으니(슬롯 하나가 새로 채워짐) 전체 재바인딩
            BindData();
        }

        //이미 화면에 보이는 슬롯이면 전체 재바인딩 없이 카운트만 갱신
        SetSlotCount(iIdx, _iCount);
        OnAddEvt?.Invoke(_SOData);

        return true;
    }

    public SOData FindData(SOData _SOData , int _iCategoryIdx = 0)
    {
        CategoryData pCategoryData = GetCategoryData(_iCategoryIdx);
        if (pCategoryData == null)
            return null;

        var listData = pCategoryData.ListData;
        for(int i = 0; i< listData.Count; ++i)
        {
            if (listData[i] == _SOData)
                return _SOData;
        }

        return null;
    }


    //카운트(레벨)는 CategoryData에 저장하지 않고 FeatureManager가 유일한 소스이므로,
    //데이터 idx를 그리고 있는 슬롯을 찾아 값만 밀어줌 (안 보이는 슬롯이면 무시)
    private void SetSlotCount(int _iDataIdx, int _iCount)
    {
        for (int i = 0; i < m_listView.Count; ++i)
        {
            if (m_listView[i].SlotIdx == _iDataIdx)
            {
                m_listView[i].SetCount(_iCount);
                return;
            }
        }
    }

  
    //에디터에서 가장 처음에 실행
    public void Build()
    {
        ClearData();

        m_refRectMask = GetComponent<RectMask2D>();
        m_refRectMask.padding = new Vector4(m_vPadding.x, m_vPadding.y, m_vPadding.x, m_vPadding.y);

        //슬롯 최대치 보정
        ClampSlot();

        Sort();

        m_iCategoryCount = m_listCategoryData.Count;

        //남은 슬롯 수 체크
        for (int i = 0; i < m_listCategoryData.Count; ++i)
        {
            int iRemnantData = 0;
            for (int j = 0; j < m_listCategoryData[i].ListData.Count; ++j)
            {
                if (m_listCategoryData[i].ListData[j] == null)
                    ++iRemnantData;
            }
            m_listCategoryData[i].m_iCurrentRemnantData = iRemnantData;
            m_listCategoryData[i].m_iCategoryIdx = i;
        }

        //슬롯 바인딩
        BindData(m_iCurrentCategoryIdx);
    }

    //슬롯 최대 계수 지정 어차피 보여질 부분만 만들기 때문에 불필요하게 더 늘리지 않기
    private void ClampSlot()
    {
        float fViewWidth = m_refContainerView.rect.width;
        float fViewHeight = m_refContainerView.rect.height;

        float fLeft = m_vPadding.x;
        float fRight = m_vPadding.x;
        float fTop = m_vPadding.y;
        float fBot = m_vPadding.y;

        Vector2 vStep = m_vSlotSize + m_vStep;

        int iMaxCols = Mathf.Max(1, Mathf.FloorToInt((fViewWidth - fLeft - fRight + m_vStep.x) / vStep.x));
        int iMaxRows = Mathf.Max(1, Mathf.FloorToInt((fViewHeight - fTop - fBot + m_vStep.y) / vStep.y));

        if (m_iSlotColCount > iMaxCols)
            m_iSlotColCount = iMaxCols;
        if (m_iSlotRowCount > iMaxRows)
            m_iSlotRowCount = iMaxRows;
    }

    public void Sort()
    {
        var refCategory = GetCategoryData(m_iCurrentCategoryIdx);
        int iCurMax = m_iSlotColCount * m_iSlotRowCount;


        var listData = refCategory.ListData;

        if (iCurMax <= listData.Count)
            m_iRowCount = m_iSlotRowCount > 0 ? m_iSlotRowCount + 1 : 0; //부드럽게 이동을 위한 뒤에 버퍼까지 계산
        else
            m_iRowCount = m_iSlotRowCount;

        m_iColCount = m_iSlotColCount;

        if (m_iRowCount * m_iColCount > listData.Count)
            m_iRowCount = Mathf.CeilToInt((float)listData.Count / m_iColCount);

        //슬롯 프리팹 생성
        Vector2 vPadding = m_vPadding;

        vPadding.x = m_vSlotSize.x / 2.0f + m_vPadding.x;
        vPadding.y = m_vSlotSize.y / 2.0f + m_vPadding.y;

        Vector2 vStep = m_vSlotSize + m_vStep;

        for (int i = 0; i < m_iRowCount; ++i)
        {
            for (int j = 0; j < m_iColCount; ++j)
            {
                if (i * m_iColCount + j >= listData.Count)
                    break;

                SlotView pSlot = Instantiate(m_refSlotPrefab, m_refContentView);
                pSlot.Init(this);

                var pRect = (RectTransform)pSlot.transform;

                pRect.anchoredPosition = new Vector2(
                    vPadding.x + vStep.x * j,
                    -vPadding.y - vStep.y * i
                );

                pRect.sizeDelta = m_vSlotSize;
                m_listView.Add(pSlot);
            }
        }

        //현재 카테고리 데이터 기준 스크롤 가능 y축 계산
        int iRowSize = listData.Count / m_iColCount;


        //보여주는 구간 밑에 얼마나 내릴 수 있는지 체크
        int iViewCount = (m_iSlotRowCount* m_iSlotColCount) / m_iSlotColCount;
        iRowSize -= iViewCount;

        if (iRowSize < 0)
            iRowSize = 0;

        m_vContaninerSize.x = vStep.x * m_iColCount;
        m_vContaninerSize.y = vStep.y * iRowSize;

    }

    public void BindData(int _iCategoryIdx = 0)
    {
        CategoryData pCategoryData = GetCategoryData(_iCategoryIdx);
        if (pCategoryData == null || pCategoryData.ListData == null)
            return;

        var listData = pCategoryData.ListData;
        ICountable ICountSource = pCategoryData.CountSource;

        //보이는 구간 업데이트
        int iStartIdx = m_iCurRow * m_iColCount;


        for (int i = 0; i < m_listView.Count; ++i)
        {
            if (iStartIdx + i >= listData.Count)
                return;

            int iDataIdx = iStartIdx + i;
            SOData refFeat = listData[iDataIdx];

            //카테고리별 카운트 출처(ICountable)에서 조회. 미할당이면 카운트 없이(0) 표시
            int iCount = 1;
            if(ICountSource != null)
                     iCount = ICountSource.GetCount(refFeat);
           
            m_listView[i].Bind(refFeat, iDataIdx, iCount);
        }
    }

    public void ClearData(int _iCategoryData = 0)
    {
        var listData = m_listCategoryData[_iCategoryData].ListData;
        for (int i = 0; i < listData.Count; ++i)
            listData[i] = null;

        m_listCategoryData[_iCategoryData].m_iCurrentRemnantData = listData.Count;
        BindData(_iCategoryData);
    }

  

    /*/////////////////////////////////////
                  Input 
    *///////////////////////////////////////

    public override void OnBeginDrag(PointerEventData e)
    {
        if (m_refFrameImage != null)
            m_refFrameImage.enabled = false;

        m_vDragCurPosition = e.position;
    }

    public override void OnDrag(PointerEventData e)
    {
        //if (m_bOnSelect == true)
        //    return;

        Vector2 vPositionDelta = e.position - m_vDragCurPosition;

        m_vDragCurPosition = e.position;

        m_vViewCurDrageLine += new Vector2(0, vPositionDelta.y);      //view 입장에서 위치
        m_vContainerDragPosition += new Vector2(0, vPositionDelta.y); //컨테이너 전체 입장에서 위치

        //스탭 오류 수정
        Vector2 vStep = m_vSlotSize + m_vStep;

        //나머지 연산 = 내 컨테이너 전체 사이즈에서 현재까지 움직인 양
        m_vContainerDragPosition.y = Mathf.Clamp(m_vContainerDragPosition.y, 0f, m_vContaninerSize.y);

        // --- 현재 행/뷰 오프셋 계산 ---
        // 행(0-base)
        int iRow = Mathf.FloorToInt(m_vContainerDragPosition.y / vStep.y);

        // % 대신 Repeat을 쓰면 안전
        float fContentPosY = Mathf.Repeat(m_vContainerDragPosition.y, vStep.y);

        m_vViewCurDrageLine = new Vector2(0.0f, fContentPosY);

       
        // 실제 이동
        m_refContentView.anchoredPosition = new Vector2(m_vViewOriginPos.x, m_vViewOriginPos.y + m_vViewCurDrageLine.y);

        if (m_iCurRow != iRow)
        {
            m_iCurRow = iRow;
            BindData(m_iCurrentCategoryIdx);
        }
    }
    public void SetTargetSlot(SlotView _pTargetSlot)
    {
        if (_pTargetSlot.SOFeat == null)
            return;

        //해당 슬롯에 프레임 장착
        if (m_refFrameImage != null)
        {
            m_refFrameImage.enabled = true;
            MoveFrameToSlot(_pTargetSlot.GetComponent<RectTransform>());
        }

        m_refTargetSlot = _pTargetSlot;

        //콜백함수
        OnSelectEvt?.Invoke(_pTargetSlot.SOFeat);
    }

    public SlotView GetTargetSlot()
    {
        return m_refTargetSlot;
    }

    public void MoveFrameToSlot(RectTransform _pSlotRect)
    {
        // 프레임 설정
        m_refFrameRectTrasnform.anchoredPosition = _pSlotRect.anchoredPosition;
        m_refFrameRectTrasnform.SetAsLastSibling(); // 항상 위로
    }



    public void ClearTarget()
    {
        if (m_refFrameImage != null)
            m_refFrameImage.enabled = false;

        m_refTargetSlot = null;
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

        m_listView.Clear();
    }

    /*/////////////////////////////////////
                  Data Category
    *////////////////////////////////////

    public List<SOData> GetListData(int _iCategoryIdx = 0)
    {

        CategoryData refCategoryData = GetCategoryData(_iCategoryIdx);
        if (refCategoryData == null)
            return null;

        if (refCategoryData.ListData == null)
            return null;

        return refCategoryData.ListData;
    }


    public CategoryData GetCategoryData(int _iCategoryIdx = 0)
    {
        if (_iCategoryIdx >= m_listCategoryData.Count)
            return null;

        return m_listCategoryData[_iCategoryIdx];
    }

    public SOData GetDataIdx(int _iDataIdx, int _iCategoryIdx)
    {
        var listData = GetListData(_iCategoryIdx);
        if (listData == null || listData[_iDataIdx] == null)
            return null;

        return listData[_iDataIdx];
    }
    public void Resize(int _iCount, int _iCategoryIdx = 0)
    {
        CategoryData refCategoryData = GetCategoryData(_iCategoryIdx);
        if (refCategoryData == null)
            return;
        var listData = refCategoryData.ListData;

        if (listData.Count > _iCount)
        {
            int iDeleteCount = listData.Count - _iCount;

            // 잘려나갈 구간에 실제 데이터가 있었다면 삭제 이벤트로 알림
            for (int i = _iCount; i < listData.Count; ++i)
            {
                if (listData[i] != null)
                    OnDeleteEvt?.Invoke(listData[i]);
            }

            // _iCount 번 인덱스부터 iDeleteCount 개수만큼 삭제
            listData.RemoveRange(_iCount, iDeleteCount);
            refCategoryData.m_iCurrentRemnantData -= iDeleteCount;
        }
        else if (listData.Count < _iCount)
        {
            int iAddCount = _iCount - listData.Count;

            for (int i = 0; i < iAddCount; ++i)
                listData.Add(null);

            refCategoryData.m_iCurrentRemnantData += iAddCount;
        }

        Build();
        //BindData(_iCategoryIdx);
    }

    public void ChanageCategory(int _iCategoryIdx)
    {
        if (m_iCategoryCount <= _iCategoryIdx)
            return;

        m_iCurrentCategoryIdx = _iCategoryIdx;
        BindData(m_iCurrentCategoryIdx);
    }

    public bool IsCanDuplication(int _iCategoryIdx)
    {
        CategoryData refCategoryData = GetCategoryData(_iCategoryIdx);
        if (refCategoryData == null)
            return false;

        return refCategoryData.IsCanDuplication;
    }

}