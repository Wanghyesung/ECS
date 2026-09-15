using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;



/*///////////////////////////////////////////
                 SliderImage

기능 : UI의 Fill값을 조절하여 이미지를 표현하는 역할
      목표치까지 이도하면 OnCompleted 콜백함수 호출
 *///////////////////////////////////////////

public class SliderImage : MonoBehaviour
{
    private float m_fMaxValue = 1.0f;
    private float m_fCurValue = 0.0f;
    private float m_fLerpDuration = 0.5f;

    private CancellationTokenSource m_ctsLerp;
    [SerializeField] private Image m_refImage = null;

    private readonly Subject<Unit> m_subjectFillCompleted = new();
    private readonly Subject<Unit> m_subjectFillMaxReached = new();
    public Observable<Unit> OnFillCompleted => m_subjectFillCompleted;   // 목표치가 0(빈 상태)이 되는 즉시 (예: HP 소진 -> 사망)
    public Observable<Unit> OnFillMaxReached => m_subjectFillMaxReached; // fill 애니메이션이 Max까지 다 찬 뒤 (예: EXP 만땅 -> 레벨업)

    private void Awake()
    {
        if(m_refImage == null)
            m_refImage = GetComponent<Image>();
    }
    
    public void SetRange(float _fMaxValue, float _fCurValue)
    {
        m_fMaxValue = _fMaxValue;
        m_fCurValue = _fCurValue;
     
        if (m_refImage != null && m_fMaxValue > 0)
            m_refImage.fillAmount = m_fCurValue / m_fMaxValue;
    }

    // 외부에서 새로운 현재값을 받아 슬라이더를 업데이트하는 함수
    public void UpdateSlider(float _fNewValue, float _fMaxValue)
    {
        if (m_ctsLerp != null)
        {
            m_ctsLerp.Cancel();
            m_ctsLerp.Dispose();
        }

        // 데이터 갱신
        m_fCurValue = _fNewValue;

        float fTargetFill = _fNewValue / _fMaxValue;
        if (fTargetFill <= 0.0f)
            m_subjectFillCompleted.OnNext(Unit.Default);

        m_ctsLerp = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        LerpSliderAsync(m_refImage.fillAmount, fTargetFill, m_ctsLerp.Token).Forget();
    }

    private async UniTaskVoid LerpSliderAsync(float _fStartFill, float _fEndFill, CancellationToken _ct)
    {
        float fElapsed = 0f;

        while (fElapsed < m_fLerpDuration )
        {
            fElapsed += Time.deltaTime;

            // 0에서 1 사이의 진행 비율 계산 (Clamped)
            float fProgress = Mathf.Clamp01(fElapsed / m_fLerpDuration);

            // 시작 fillAmount에서 목표 fillAmount까지 보간
            m_refImage.fillAmount = Mathf.Lerp(_fStartFill, _fEndFill, fProgress);

            await UniTask.Yield(_ct);
        }

        // 애니메이션이 실제로 Max까지 다 찬 시점에만 호출 (UpdateSlider 호출 즉시가 아님)
        if (_fEndFill >= 1.0f)
        {
            m_subjectFillMaxReached.OnNext(Unit.Default);
            m_refImage.fillAmount = 0.0f;
        }
        else
             m_refImage.fillAmount = _fEndFill;

        m_ctsLerp.Dispose();
        m_ctsLerp = null;
    }

}
