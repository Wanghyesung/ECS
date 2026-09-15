using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class LoadingOverlay : MonoBehaviour
{
    [SerializeField] private Slider m_refLoadingSlider;
    private CancellationTokenSource m_ctsFill;

    [SerializeField] private float m_fFillSpeed = 1.0f;
    private float m_fTargetFill = 0.0f;

    public void SetProgress(float _fValue)
    {
        if (m_refLoadingSlider == null)
            return;

        m_fTargetFill = Mathf.Clamp01(_fValue);

        if (m_ctsFill != null)
        {
            m_ctsFill.Cancel();
            m_ctsFill.Dispose();
        }
        m_ctsFill = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        SmoothFillAsync(m_ctsFill.Token).Forget();
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

    private async UniTaskVoid SmoothFillAsync(CancellationToken _ct)
    {
        while (true)
        {
            if (m_refLoadingSlider == null)
                return;

            float fCurAmount = m_refLoadingSlider.value;

            // 지정한 속도로 target 쪽으로 이동
            float fNextAmount = Mathf.MoveTowards(fCurAmount, m_fTargetFill, m_fFillSpeed * Time.unscaledDeltaTime
            );

            m_refLoadingSlider.value = fNextAmount;

            if (m_fTargetFill >= 0.99f)
                break;

            await UniTask.Yield(_ct);
        }

        m_ctsFill.Dispose();
        m_ctsFill = null;
    }
}
