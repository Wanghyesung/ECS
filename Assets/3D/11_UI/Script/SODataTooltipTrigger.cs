using R3;
using UnityEngine;

/*///////////////////////////////////////////
            ITooltipDataable
기능 : 호버 설명창에 보여줄 SOData 를 내주는 쪽(슬롯, 카드)이 구현
 *///////////////////////////////////////////

public interface ITooltipDataable
{
    SOData TooltipData { get; }   //비었거나 아직 공개 전이면 null
}

/*///////////////////////////////////////////
            SODataTooltipTrigger
기능 : 같은 오브젝트의 BaseButtonUI 에서 마우스 진입/이탈을 받아,
       ITooltipDataable 이 내준 SOData 를 SODataTooltipView 에 띄우고 닫는다.
       설명을 띄우고 싶은 UI 에 이 컴포넌트만 붙이면 된다
 *///////////////////////////////////////////

[RequireComponent(typeof(BaseButtonUI))]
public sealed class SODataTooltipTrigger : MonoBehaviour
{
    private BaseButtonUI m_refButton = null;
    private ITooltipDataable m_refProvider = null;

    private bool m_bPointerInside = false;

    private void Awake()
    {
        m_refButton = GetComponent<BaseButtonUI>();
        m_refProvider = GetComponent<ITooltipDataable>();

#if UNITY_EDITOR
        if (m_refProvider == null)
            Debug.LogError($"[{name}] SODataTooltipTrigger 가 ITooltipDataable 없는 오브젝트에 붙음", this);
#endif

        //같은 오브젝트의 Subject 라 순서 문제가 없고, 구독 해제는 AddTo 가 파괴 시점에 대신한다
        m_refButton.OnEnterEvt.Subscribe(_ => Enter()).AddTo(this);
        m_refButton.OnExitEvt.Subscribe(_ => Exit()).AddTo(this);
    }

    //SetActive(false) 로 사라지는 UI 에는 PointerExit 가 오지 않아 창이 남는다
    private void OnDisable()
    {
        Exit();
    }

    //데이터만 바뀌는 경우(슬롯 재사용, 카드 공개)는 Enter 가 다시 오지 않으므로 소유자가 직접 호출
    public void Refresh()
    {
        if (m_bPointerInside == false)
            return;

        SOData refData = m_refProvider == null ? null : m_refProvider.TooltipData;
        if (refData == null)
            SODataTooltipView.Hide();
        else
            SODataTooltipView.Show(refData, (RectTransform)transform);
    }

    private void Enter()
    {
        m_bPointerInside = true;
        Refresh();
    }

    private void Exit()
    {
        if (m_bPointerInside == false)
            return;

        m_bPointerInside = false;
        SODataTooltipView.Hide();
    }
}
