using System.Collections;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
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
    
    private Monster m_refCurTarget = null;
    private IDisposable m_disposableHp;
    private CancellationTokenSource m_ctsHide;

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
    }

    public void Show(Monster _refTarget)
    {
        if (_refTarget != m_refCurTarget)
        {
            m_refCurTarget = _refTarget;
            m_disposableHp?.Dispose();
            m_refSlider.SetRange(_refTarget.MaxHp, _refTarget.Hp.CurrentValue);
            m_disposableHp = _refTarget.Hp.Subscribe(_lHp => m_refSlider.UpdateSlider(_lHp, _refTarget.MaxHp));
        }

        if (m_refRoot != null)
            m_refRoot.SetActive(true);

        if (m_ctsHide != null)
        {
            m_ctsHide.Cancel();
            m_ctsHide.Dispose();
        }
        m_ctsHide = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        HideAfterDelayAsync(m_ctsHide.Token).Forget();
    }

    private async UniTaskVoid HideAfterDelayAsync(CancellationToken _ct)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(m_fHideDelay), cancellationToken: _ct);

        if (m_refRoot != null)
            m_refRoot.SetActive(false);
        m_disposableHp?.Dispose();
        m_disposableHp = null;
        m_refCurTarget = null;
        m_ctsHide.Dispose();
        m_ctsHide = null;
    }
}
