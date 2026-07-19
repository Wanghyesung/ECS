using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;


/*///////////////////////////////////////////
                BaseButtonUI
기능 : 해당 UI를 눌렀을 때 콜백 기능을 담당하는 클래스
 *///////////////////////////////////////////

public class BaseButtonUI : MonoBehaviour,
      IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler
{

    public TextMeshProUGUI m_pTextMeshProUGUI;

    //코드 바인딩용 델리게이트(원하면 사용) 
    public event Action OnEnterEvt;
    public event Action OnExitEvt;
    public event Action OnDownEvt;
    public event Action OnUpEvt;
    public event Action OnBeginDragEvt;
    public event Action OnDragEvt;
    public event Action OnEndDragEvt;
    public event Action OnClickEvt;

    // 인스펙터 바인딩용 
    [SerializeField] private UnityEvent OnEnterUEvt;
    [SerializeField] private UnityEvent OnExitUEvt;
    [SerializeField] private UnityEvent OnDownUEvt;
    [SerializeField] private UnityEvent OnUpUEvt;
    [SerializeField] private UnityEvent OnBeginDragUEvt;
    [SerializeField] private UnityEvent OnDragUEvt;
    [SerializeField] private UnityEvent OnEndDragUEvt;
    [SerializeField] private UnityEvent OnClickUEvt;

    //[SerializeField] private SOAudio m_pClickAudio;
    //[SerializeField] private SOAudio m_pDownAudio;
    

    virtual public void OnPointerEnter(PointerEventData e)
    {
        OnEnterUEvt?.Invoke();
        OnEnterEvt?.Invoke();
    }

    virtual public void OnPointerExit(PointerEventData e)
    {
        OnExitUEvt?.Invoke();
        OnExitEvt?.Invoke();
    }

    virtual public void OnPointerDown(PointerEventData e)
    {
        //if (m_pDownAudio != null)
        //    SoundManager.m_Instance.PlaySfx(m_pDownAudio, null);
        OnDownUEvt?.Invoke();
        OnDownEvt?.Invoke();

    }

    virtual public void OnPointerUp(PointerEventData e)
    {
        OnUpUEvt?.Invoke();
        OnUpEvt?.Invoke();

    }

    virtual public void OnBeginDrag(PointerEventData e)
    {
        OnBeginDragUEvt?.Invoke();
        OnBeginDragEvt?.Invoke();
    }

    virtual public void OnDrag(PointerEventData e)
    {
        OnDragUEvt?.Invoke();
        OnDragEvt?.Invoke();
    }

    virtual public void OnEndDrag(PointerEventData e)
    {
        OnEndDragUEvt?.Invoke();
        OnEndDragEvt?.Invoke();
    }
    virtual public void OnPointerClick(PointerEventData e)
    {
        OnClickUEvt?.Invoke();
        OnClickEvt?.Invoke();
    }
}