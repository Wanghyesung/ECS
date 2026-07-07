using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SilderUI : MonoBehaviour
{
    private float m_fMaxValue = 1.0f;
    private float m_fCurValue = 0.0f;

    [SerializeField] private float m_fCoSecond = 0.5f;

    private Coroutine m_refCoroutine = null;
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
    }

    public void UpdateSlider(float _fCurValue)
    {
        if(m_refCoroutine != null)
            StopCoroutine(m_refCoroutine);

        m_refCoroutine = StartCoroutine(StartLerp(m_fCurValue, _fCurValue));
    }


    private IEnumerator StartLerp(float _fStartValue, float _fEndValue)
    {
        float fStartValue = _fStartValue;

        //while(fStartValue >= _fEndValue)
        //{

        //}

        yield return null;
    }
    
}
