using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnableStopTime : MonoBehaviour
{
    private void OnEnable()
    {
        TimeScaleManager.m_Instance.Pause(this);
    }

    private void OnDisable()
    {
        TimeScaleManager.m_Instance.Resume(this);
    }
}
