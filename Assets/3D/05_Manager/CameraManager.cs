using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraManager : MonoBehaviour
{
    public static CameraManager m_Instance = null;

    [SerializeField] private Camera m_refMainCamera;
    [SerializeField] private Transform m_refPlayer;
    [SerializeField] private Vector3 m_vOffset = new Vector3(0.0f, 5.0f, -10.0f);

    [SerializeField] private Image m_refBloodScreen = null;
    private Color m_tBloodColor = Color.white;

    private Vector3 m_vShakeOffset = Vector3.zero;
    private Coroutine m_CoShake = null;
    private void Awake()
    {
        if (m_Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        m_tBloodColor = m_refBloodScreen.color;
    }

    private void LateUpdate()
    {
        Vector3 vPosition = m_refPlayer.position + m_refPlayer.rotation * m_vOffset;

        m_refMainCamera.transform.position = vPosition + m_vShakeOffset;
        m_refMainCamera.transform.rotation = m_refPlayer.rotation;
    }

    //진폭, 흔들리는 속도
    public void StartShakeCamera(float _fMagnitude, float _fDuration = 0.2f)
    {
        if(m_CoShake != null)
            StopCoroutine(m_CoShake);

        m_CoShake = StartCoroutine(COShakeRoutine(_fMagnitude, _fDuration));
    }

    private IEnumerator COShakeRoutine(float _fMagnitude, float _fDuration)
    {
        float fElapsed = 0f;
        while (fElapsed < _fDuration)
        {
            // Random.insideUnitSphere를 쓰면 사방으로 튀는 벡터를 줍니다.
            m_vShakeOffset = Random.insideUnitSphere * _fMagnitude;

            if(m_refBloodScreen != null)
            {
                float fCurAlpha = Mathf.Lerp(1.0f, 0f, fElapsed / _fDuration);
                m_tBloodColor.a = fCurAlpha;
                m_refBloodScreen.color = m_tBloodColor;
            }

            fElapsed += Time.deltaTime;
            yield return null;
        }
        // 흔들림 끝났으면 0으로 초기화해서 원래 자리로 복귀
        m_vShakeOffset = Vector3.zero;
        m_CoShake = null;
    }

}
