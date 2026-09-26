using R3;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using DG.Tweening;

/*///////////////////////////////////////////
기능 : 해당 UI를 눌렀을 때 콜백 기능을 담당하는 클래스
 *///////////////////////////////////////////

public class BaseButtonUI : MonoBehaviour,
      IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler
{

    //코드 바인딩용 델리게이트(원하면 사용) 
    private readonly Subject<Unit> m_subjectEnter = new();
    private readonly Subject<Unit> m_subjectExit = new();
    private readonly Subject<Unit> m_subjectDown = new();
    private readonly Subject<Unit> m_subjectUp = new();
    private readonly Subject<Unit> m_subjectBeginDrag = new();
    private readonly Subject<Unit> m_subjectDrag = new();
    private readonly Subject<Unit> m_subjectEndDrag = new();
    private readonly Subject<Unit> m_subjectClick = new();
    public Observable<Unit> OnEnterEvt => m_subjectEnter;
    public Observable<Unit> OnExitEvt => m_subjectExit;
    public Observable<Unit> OnDownEvt => m_subjectDown;
    public Observable<Unit> OnUpEvt => m_subjectUp;
    public Observable<Unit> OnBeginDragEvt => m_subjectBeginDrag;
    public Observable<Unit> OnDragEvt => m_subjectDrag;
    public Observable<Unit> OnEndDragEvt => m_subjectEndDrag;
    public Observable<Unit> OnClickEvt => m_subjectClick;

    // 인스펙터 바인딩용 
    [SerializeField] private UnityEvent OnEnterUEvt;
    [SerializeField] private UnityEvent OnExitUEvt;
    [SerializeField] private UnityEvent OnDownUEvt;
    [SerializeField] private UnityEvent OnUpUEvt;
    [SerializeField] private UnityEvent OnBeginDragUEvt;
    [SerializeField] private UnityEvent OnDragUEvt;
    [SerializeField] private UnityEvent OnEndDragUEvt;
    [SerializeField] private UnityEvent OnClickUEvt;

    [Header("Audio")]
    [SerializeField] private SOAudio m_SOClickAudio;   // 비워두면 무음 - 버튼별로 다른 소리/무음 가능

   
    virtual public void OnPointerExit(PointerEventData e)
    {
        OnExitUEvt?.Invoke();
        m_subjectExit.OnNext(Unit.Default);
    }
    virtual public void OnPointerEnter(PointerEventData e)
    {
        OnEnterUEvt?.Invoke();
        m_subjectEnter.OnNext(Unit.Default);
    }
    public void OnPointerUp(PointerEventData _eventData)
    {
        OnUpUEvt?.Invoke();
        m_subjectUp.OnNext(Unit.Default);
    }

    virtual public void OnBeginDrag(PointerEventData e)
    {
        OnBeginDragUEvt?.Invoke();
        m_subjectBeginDrag.OnNext(Unit.Default);
    }

    virtual public void OnDrag(PointerEventData e)
    {
        OnDragUEvt?.Invoke();
        m_subjectDrag.OnNext(Unit.Default);
    }

    virtual public void OnEndDrag(PointerEventData e)
    {
        OnEndDragUEvt?.Invoke();
        m_subjectEndDrag.OnNext(Unit.Default);
    }
    virtual public void OnPointerClick(PointerEventData e)
    {
        PlayClickAudio();
        OnClickUEvt?.Invoke();
        m_subjectClick.OnNext(Unit.Default);
    }

    // base.OnPointerClick 을 부르지 않는 파생(SlotView)도 소리만 낼 수 있게 분리
    protected void PlayClickAudio()
    {
        if (m_SOClickAudio != null)
            SoundManager.m_Instance.PlaySfx(m_SOClickAudio);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDownUEvt?.Invoke();
        m_subjectDown.OnNext(Unit.Default);
    }


    public void PushScale()
    {
        transform.DOPunchScale(new Vector3(0.2f, 0.2f, 0), 0.2f, 1, 0.5f);
    }

      
}