using TMPro;
using UnityEngine;

//선택된 SOData의 설명을 TMP로 그려주는 뷰 (Container.OnSelectEvt 구독, 느슨한 결합 유지)
public class SODescView : MonoBehaviour
{
    [SerializeField] private Container m_refContainer;
    [SerializeField] private TextMeshProUGUI m_refDescText;

    private void OnEnable()
    {
        if (m_refContainer != null)
        {
            m_refContainer.Init();
            m_refContainer.OnSelectEvt += OnSelect;
        }

    }

    private void OnDisable()
    {
        if (m_refContainer != null)
            m_refContainer.OnSelectEvt -= OnSelect;
    }

    private void OnSelect(SOData _refSO)
    {
        if (m_refDescText == null)
            return;

        m_refDescText.text = _refSO != null ? _refSO.Description : string.Empty;
    }
}
