using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private Camera m_refMainCamera;
    [SerializeField] private Transform m_refPlayer;
    [SerializeField] private Vector3 m_vOffset = new Vector3(0.0f, 5.0f, -10.0f);

    private void LateUpdate()
    {
        m_refMainCamera.transform.position = m_refPlayer.position + m_refPlayer.rotation * m_vOffset;

        m_refMainCamera.transform.rotation = m_refPlayer.rotation;
    }
}
