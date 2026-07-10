using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                MonsterHPBar
기능 : 현재 공격 중인 몬스터 한 마리의 HP만 보여주는 단일 HP바
 *///////////////////////////////////////////

public class MonsterHPBar : MonoBehaviour
{
    public static MonsterHPBar m_Instance = null;

    [SerializeField] private GameObject m_refRoot;
    [SerializeField] private SliderImage m_refSlider;
    [SerializeField] private float m_fHideDelay = 2.0f;
    
    private WaitForSeconds m_refWaitSecond = null; 
    private Monster m_refCurTarget = null;
    private Coroutine m_CoHide = null;

    private void Awake()
    {
        if (m_Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        m_Instance = this;

        if (m_refRoot != null)
            m_refRoot.SetActive(false);

        m_refWaitSecond = new WaitForSeconds(m_fHideDelay);
    }

    public void ShowHp(Monster _refTarget, long _lCurrentHp, long _lMaxHp)
    {
        if (_refTarget != m_refCurTarget)
        {
            m_refCurTarget = _refTarget;
            m_refSlider.SetRange(_lMaxHp, _lCurrentHp);
        }
        else
        {
            m_refSlider.UpdateSlider(_lCurrentHp);
        }

        if (m_refRoot != null)
            m_refRoot.SetActive(true);

        if (m_CoHide != null)
            StopCoroutine(m_CoHide);
        m_CoHide = StartCoroutine(CoHideAfterDelay());
    }

    private IEnumerator CoHideAfterDelay()
    {
        yield return m_refWaitSecond;

        m_refRoot?.SetActive(false);
        m_refCurTarget = null;
        m_CoHide = null;
    }
}
