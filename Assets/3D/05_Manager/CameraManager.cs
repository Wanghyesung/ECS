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

    private Vector3 m_vOriginPos;
    //[SerializeField] private float m_fOrthographicSize = 10.0f;

    private Quaternion m_qFixedRotation;

    [SerializeField] private Image m_refBloodScreen = null;
    private Color m_tBloodColor = Color.white;

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

        m_vOriginPos = m_refMainCamera.transform.position;
        m_refMainCamera.orthographic = true;
    }

    private void LateUpdate()
    {
        // 더 이상 플레이어 회전을 따라가지 않고, 고정된 오프셋/각도로 플레이어를 쫓아가기만 함
        Vector3 vPosition = m_refPlayer.position;
        vPosition.y = m_vOriginPos.y;

        m_refMainCamera.transform.position = vPosition;
    }

    // 카메라 위치는 더 이상 흔들지 않고, 피격 시 화면 붉은 플래시(블러드 스크린)만 재생
    public void StartShakeCamera(float _fDuration = 0.2f)
    {
        if(m_CoShake != null)
            StopCoroutine(m_CoShake);

        m_CoShake = StartCoroutine(COShakeRoutine(_fDuration));
    }

    private IEnumerator COShakeRoutine(float _fDuration)
    {
        float fElapsed = 0f;
        while (fElapsed < _fDuration)
        {
            if(m_refBloodScreen != null)
            {
                float fCurAlpha = Mathf.Lerp(1.0f, 0f, fElapsed / _fDuration);
                m_tBloodColor.a = fCurAlpha;
                m_refBloodScreen.color = m_tBloodColor;
            }

            fElapsed += Time.deltaTime;
            yield return null;
        }

        m_CoShake = null;
    }

}
