using R3;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AimView : MonoBehaviour
{
    [SerializeField] private Image m_refAimImage;

    private IDisposable m_refOnTargetDisposer;
    private IDisposable m_refUnTargetDisposer;

    private void Start()
    {
        if (Player.CurrentPlayer == null)
            return;

        Aim refAim = Player.CurrentPlayer.Aim;

        m_refOnTargetDisposer = refAim.OnTarget.Subscribe(_ => SetOnTargetColor()).AddTo(this);
        m_refUnTargetDisposer = refAim.UnTarget.Subscribe(_ => SetUnTargetColor()).AddTo(this);
    }

    private void OnDestroy()
    {
        m_refOnTargetDisposer.Dispose();
        m_refUnTargetDisposer.Dispose();
    }
    private void SetOnTargetColor()
    {
        if (m_refAimImage == null)
            return;

        m_refAimImage.color = Color.red;
    }

    private void SetUnTargetColor()
    {

        if (m_refAimImage == null)
            return;

        m_refAimImage.color = Color.white;
    }
}
