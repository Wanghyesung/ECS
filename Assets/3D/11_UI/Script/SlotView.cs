using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//(이벤토리 슬롯)
public class SlotView : BaseButtonUI
{
    //하이라이트도 나중에 추가
    [SerializeField] protected Image m_refIcon = null;
    [SerializeField] protected TextMeshProUGUI m_refCountBadgeText = null;
    [SerializeField] private Color m_tStartColor = Color.white;

    protected int m_iID = -1;
    protected int m_iSlotIdx = -1; //내 컨테이너에서 몇번째 슬롯인지
    protected int m_iCount = 0;
    public int Count => m_iCount;

    protected SOData m_SOTargetSO = null;

    protected ISelectDataable m_refContainer = null;

    public SOData SOData { get => m_SOTargetSO; }
    public int SlotIdx { get => m_iSlotIdx; }

    public void Init(ISelectDataable _refContainer)
    {
        m_refContainer = _refContainer;

        //만약 Icon이 없다면 터트리게
        if (m_refIcon == null)
            m_refIcon = GetComponent<Image>();

        SetTierColor(m_tStartColor);
    }

    
    public virtual void Bind(SOData _SOFeat, int _iSlotIdx, int _iCount = 0)
    {
        BindData(_SOFeat, _iSlotIdx, _iCount);
    }

    override public void OnBeginDrag(PointerEventData e)
    {
        base.OnBeginDrag(e);
        m_refContainer.OnBeginDrag(e);
    }

    override public void OnDrag(PointerEventData e)
    {
        base.OnDrag(e);
        m_refContainer.OnDrag(e);
    }

    override public void OnEndDrag(PointerEventData e)
    {
        base.OnEndDrag(e);
        m_refContainer.OnEndDrag(e);
    }

    public override void OnPointerClick(PointerEventData e)
    {
        //if (m_refContainer.IsOnSelect == false)
        //    return;

        if (m_refContainer != null)
            m_refContainer.SetTargetSlot(this);
    }

    protected void BindData(SOData _SOFeat, int _iSlotIdx, int _iCount = 0)
    {

        if (_SOFeat != null)
        {
            m_SOTargetSO = _SOFeat;

            m_refIcon.sprite = _SOFeat.Icon;
            m_refIcon.enabled = true;

            //m_iID = _pEntryUI.Id;
        }
        else
        {
            m_SOTargetSO = null;
            m_refIcon.enabled = false;
            m_iID = -1;
        }

        SetCount(_iCount);
        m_iSlotIdx = _iSlotIdx;
    }

    //등급 색상 표시 여부/색상은 이 슬롯을 쓰는 Container가 결정해서 밀어줌 (SlotView는 그리기만 담당)
    //별도 프레임 이미지 없이 아이콘 자체를 틴트한다
    public void SetTierColor(Color _tColor)
    {
        if (m_refIcon == null)
            return;

        m_refIcon.color = _tColor;
    }

    //Container가 바인딩 시점/흭득 이벤트에서 밀어주는 카운트를 받아 뱃지에 반영
    public void SetCount(int _iCount)
    {
        if (m_refCountBadgeText == null)
            return;

        m_iCount = _iCount;
        bool bShow = _iCount > 1; // 1개 이하면 보통 표기 안 함
        m_refCountBadgeText.enabled = bShow;

        if (bShow)
            m_refCountBadgeText.text = m_iCount.ToString();
    }


}