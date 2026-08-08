using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadingOverlay : MonoBehaviour
{
    [SerializeField] private Slider m_refLoadingSlider;
    private Coroutine m_COLoading = null;

    [SerializeField] private float m_fFillSpeed = 1.0f;
    private float m_fTargetFill = 0.0f;

    public void SetProgress(float _fValue)
    {
        if (m_refLoadingSlider == null)
            return;

        m_fTargetFill = Mathf.Clamp01(_fValue);

        // 이미 코루틴 돌고 있으면 그대로 target만 바꿔서 이어서 감
        if (m_COLoading != null)
            StopCoroutine(m_COLoading);
        m_COLoading = StartCoroutine(CoSmoothFill());
    }

    public void ShowLoadingImage()
    {
        gameObject.SetActive(true);
    }

    public void CompletedLoading()
    {
        gameObject.SetActive(false);
        m_refLoadingSlider.value = 0.0f;
    }

    private IEnumerator CoSmoothFill()
    {
        while (true)
        {
            if (m_refLoadingSlider == null)
                yield break;

            float fCurAmount = m_refLoadingSlider.value;

            // 지정한 속도로 target 쪽으로 이동
            float fNextAmount = Mathf.MoveTowards(fCurAmount, m_fTargetFill, m_fFillSpeed * Time.unscaledDeltaTime
            );

            m_refLoadingSlider.value = fNextAmount;

            if (m_fTargetFill >= 0.99f)
                break;

            yield return null;
        }

        m_COLoading = null;
    }
}
