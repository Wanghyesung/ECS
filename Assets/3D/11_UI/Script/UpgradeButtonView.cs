using System;
using TMPro;
using UnityEngine;

/*///////////////////////////////////////////
                UpgradeButtonView
목적 : "비용 표시 + 재화 부족 시 반투명 처리"를 전담하는 강화 버튼 View.
      자신의 클릭을 OnClickEvt로만 알리고, 살 수 있는지 판단은 하지 않는다
      (재화 비교/차감은 Presenter인 PlayerStatUI의 책임 — 이 View는 결과만 그린다).
 *///////////////////////////////////////////
public sealed class UpgradeButtonView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI m_refCostText;

    private const float DISABLED_ALPHA = 0.5f;

    private BaseButtonUI m_refButton;
    private CanvasGroup m_refGroup;

    public event Action OnClickEvt;

    private void Awake()
    {
        m_refButton = GetComponent<BaseButtonUI>();

        m_refGroup = GetComponent<CanvasGroup>();
        if (m_refGroup == null)
            m_refGroup = gameObject.AddComponent<CanvasGroup>();

        m_refButton.OnClickEvt += HandleClick;
    }

    private void OnDestroy()
    {
        m_refButton.OnClickEvt -= HandleClick;
    }

    private void HandleClick()
    {
        OnClickEvt?.Invoke();
    }

    public void Show(int _iCost, bool _bCanAfford)
    {
        m_refCostText.text = "UPGRADE  " + _iCost;
        m_refGroup.alpha = _bCanAfford ? 1f : DISABLED_ALPHA;
    }
}
