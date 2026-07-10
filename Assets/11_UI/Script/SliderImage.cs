using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



/*///////////////////////////////////////////
                 SliderImage

기능 : UI의 Fill값을 조절하여 이미지를 표현하는 역할
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
    public void UpdateSlider(float _fNewValue)
    {
        if (m_CoLerpSlider != null)
            StopCoroutine(m_CoLerpSlider);

        float fTargetFill = _fNewValue / m_fMaxValue;
        m_CoLerpSlider = StartCoroutine(CoLerpSlider(m_refImage.fillAmount, fTargetFill));

        // 데이터 갱신
        m_fCurValue = _fNewValue;
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

        // 완전히 목표값에 도달하도록 보장
        m_refImage.fillAmount = _fEndFill;
        m_CoLerpSlider = null;
    }

}
