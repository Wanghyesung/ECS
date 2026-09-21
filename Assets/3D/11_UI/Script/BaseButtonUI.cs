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

    //hover 설명창 - 파생 클래스가 현재 SOData 를 돌려주면 진입 시 SODataTooltipView 가 뜬다
    protected virtual SOData TooltipData => null;
    private bool m_bPointerInside = false;
    private Camera m_refEventCamera = null;

    //[SerializeField] private SOAudio m_pClickAudio;
    //[SerializeField] private SOAudio m_pDownAudio;

    //SetActive(false) 된 UI 에는 PointerExit 가 오지 않으므로 여기서 닫는다
    protected virtual void OnDisable()
    {
        m_bPointerInside = false;
        SODataTooltipView.Hide(this);
    }

    virtual public void OnPointerExit(PointerEventData e)
    {
        OnExitUEvt?.Invoke();
        m_subjectExit.OnNext(Unit.Default);

        m_bPointerInside = false;
        SODataTooltipView.Hide(this);
    }
    virtual public void OnPointerEnter(PointerEventData e)
    {
        OnEnterUEvt?.Invoke();
        m_subjectEnter.OnNext(Unit.Default);

        m_bPointerInside = true;
        m_refEventCamera = e.enterEventCamera;
        RefreshTooltip();
    }
    public void OnPointerUp(PointerEventData _eventData)
    {
        //if (m_pDownAudio != null)
        //    SoundManager.m_Instance.PlaySfx(m_pDownAudio, null);
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
        OnClickUEvt?.Invoke();
        m_subjectClick.OnNext(Unit.Default);
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

    //데이터가 바뀐 직후 파생 클래스가 호출 - 마우스가 위에 없으면 아무것도 안 함
    protected void RefreshTooltip()
    {
        if (m_bPointerInside == false)
            return;

        SOData refData = TooltipData;
        if (refData == null)
            SODataTooltipView.Hide(this);
        else
            SODataTooltipView.Show(this, refData, (RectTransform)transform, m_refEventCamera);
    }

      
}