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


    protected int m_iID = -1;
    protected int m_iSlotIdx = -1; //내 컨테이너에서 몇번째 슬롯인지
    protected SOFeature m_SOTargetSO = null;

    protected Container m_refContainer = null;

    public SOFeature SOFeat { get => m_SOTargetSO; }
    public int SlotIdx { get => m_iSlotIdx; }

    public void Init(Container _refContainer)
    {
        m_refContainer = _refContainer;

        //만약 Icon이 없다면 터트리게
        if (m_refIcon == null)
            m_refIcon = GetComponent<Image>();
    }

    public virtual void Bind(SOFeature _refFeat, int _iSlotIdx)
    {
        BindData(_refFeat, _iSlotIdx);
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
        {
            m_refContainer.SetTargetSlot(this);
        }
    }

    protected void BindData(SOFeature _SOFeat, int _iSlotIdx)
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

        m_iSlotIdx = _iSlotIdx;
    }
    //private void update_count(SOEntryUI _pEntryUI)
    //{
    //    if (m_refCountBadgeText == null)
    //        return;

    //    int iCount = m_refContainer?.GetCount(_pEntryUI) ?? 0;

    //    bool bShow = iCount > 1; // 1개 이하면 보통 표기 안 함
    //    if (bShow)
    //    {
    //        m_refCountBadgeText.enabled = true;
    //        m_refCountBadgeText.text = iCount.ToString();
    //    }
    //    else
    //        m_refCountBadgeText.enabled = false;
    //}



}