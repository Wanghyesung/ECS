using System;
using System.Collections;
using System.Collections.Generic;
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

    [SerializeField] private float m_fCoSecond = 0.5f;

    private Coroutine m_CoLerpSlider = null;
    private WaitForSeconds m_refWaitSecond = null;
    [SerializeField] private Image m_refImage = null;

    public event Action OnFillCompleted;   // 목표치가 0(빈 상태)이 되는 즉시 호출 (예: HP 소진 -> 사망)
    public event Action OnFillMaxReached;  // 실제로 fill 애니메이션이 Max까지 다 찬 뒤에 호출 (예: EXP 만땅 -> 레벨업)

    private void Awake()
    {
        if(m_refImage == null)
            m_refImage = GetComponent<Image>();

        m_refWaitSecond = new WaitForSeconds(m_fCoSecond);
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
        if (m_CoLerpSlider != null)
            StopCoroutine(m_CoLerpSlider);

        // 데이터 갱신
        m_fCurValue = _fNewValue;

        float fTargetFill = _fNewValue / _fMaxValue;
        if (fTargetFill <= 0.0f)
            OnFillCompleted?.Invoke();

        m_CoLerpSlider = StartCoroutine(CoLerpSlider(m_refImage.fillAmount, fTargetFill));
    }

    // 실제 보간을 담당하는 코루틴
    private IEnumerator CoLerpSlider(float _fStartFill, float _fEndFill)
    {
        float fElapsed = 0f;

        while (fElapsed < m_fLerpDuration)
        {
            fElapsed += Time.deltaTime;

            // 0에서 1 사이의 진행 비율 계산 (Clamped)
            float fProgress = Mathf.Clamp01(fElapsed / m_fLerpDuration);

            // 시작 fillAmount에서 목표 fillAmount까지 보간
            m_refImage.fillAmount = Mathf.Lerp(_fStartFill, _fEndFill, fProgress);

            yield return null;
        }

        // 애니메이션이 실제로 Max까지 다 찬 시점에만 호출 (UpdateSlider 호출 즉시가 아님)
        if (_fEndFill >= 1.0f)
        {
            OnFillMaxReached?.Invoke();
            m_refImage.fillAmount = 0.0f;
        }
        else
             m_refImage.fillAmount = _fEndFill;

        m_CoLerpSlider = null;
    }

}
