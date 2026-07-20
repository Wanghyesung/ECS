using UnityEngine;
using UnityEngine.EventSystems;


/*///////////////////////////////////////////
                BaseButtonUI
기능 : 해당 UI를 눌렀을 때 콜백 기능을 담당하는 클래스
 *///////////////////////////////////////////

public abstract class BaseButtonUI : MonoBehaviour, 
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] private bool m_bInteractable = true;

    public bool IsPressed { get; private set; }

    public void OnPointerDown(PointerEventData _eventData)
    {
        IsPressed = true;
        OnPressed();
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        IsPressed = false;
        OnReleased();
    }

    public void OnPointerClick(PointerEventData _eventData)
    { 
        OnClicked();
    }

    // 파생 클래스는 필요한 콜백만 오버라이드
    protected virtual void OnPressed() { }
    protected virtual void OnReleased() { }
    protected virtual void OnClicked() { }
}