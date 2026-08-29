using Cinemachine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
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
    private CancellationTokenSource m_refCTS;
    private bool m_bLock = false;

    private void Awake()
    {
        m_refCTS = new CancellationTokenSource();
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
    private void OnDestroy()
    {
        m_refCTS.Cancel();
        m_refCTS.Dispose();
    }

    private void LateUpdate()
    {
        if (m_bLock == true)
            return;

        Vector3 vPosition = m_refPlayer.position + m_refPlayer.rotation * m_vOffset;

        m_refMainCamera.transform.position = vPosition + m_vShakeOffset;
        m_refMainCamera.transform.rotation = m_refPlayer.rotation;
    }

    //진폭, 흔들리는 속도
    public void StartShakeCamera(float _fMagnitude, float _fDuration = 0.2f)
    {
        ShakeRoutine(m_refCTS.Token, _fMagnitude, _fDuration).Forget();
    }

    private async UniTaskVoid ShakeRoutine(CancellationToken _tToken ,float _fMagnitude, float _fDuration)
    {
        float fElapsed = 0f;
        while (fElapsed < _fDuration)
        {
            // Random.insideUnitSphere를 쓰면 사방으로 튀는 벡터를 줍니다.
            m_vShakeOffset = Random.insideUnitSphere * _fMagnitude;

            if (m_refBloodScreen != null)
            {
                float fCurAlpha = Mathf.Lerp(1.0f, 0f, fElapsed / _fDuration);
                m_tBloodColor.a = fCurAlpha;
                m_refBloodScreen.color = m_tBloodColor;
            }

            fElapsed += Time.deltaTime;
            await UniTask.Yield(_tToken);
        }

        // 흔들림 끝났으면 0으로 초기화해서 원래 자리로 복귀
        m_vShakeOffset = Vector3.zero;
    }

    //지정된 위치로 자연스럽게 이동, 이동이 끝난 후 다시 플레이어 시점으로 이동
    public async UniTask MoveToPoint(Vector3 _vPosition, float _fMoveTime, float _fWaitTime = 0.0f)
    {
        Transform camTransform = transform;

        // 타깃 위치를 바라보는 회전값 계산
        Quaternion targetRot = Quaternion.LookRotation(_vPosition - camTransform.position);

        var refSource = new UniTaskCompletionSource();

        // CancellationToken 등록: 취소 요청 시 UniTaskCompletionSource도 함께 취소 처리
        using (m_refCTS.Token.Register(() => refSource.TrySetCanceled(m_refCTS.Token)))
        {
            Sequence refSeq = DOTween.Sequence();
            refSeq.Append(camTransform.DOMove(_vPosition, _fMoveTime).SetEase(Ease.OutQuad));
            refSeq.Join(camTransform.DORotateQuaternion(targetRot, _fMoveTime).SetEase(Ease.OutQuad));

            // 정상 완료 시
            refSeq.OnComplete(() => refSource.TrySetResult());

            // 중간에 킬(Kill)되거나 파괴될 때도 무한 대기에 빠지지 않도록 처리
            refSeq.OnKill(() => refSource.TrySetCanceled());

            await refSource.Task;
        }

        if (_fWaitTime > 0.0f)
            await UniTask.Delay((int)(_fWaitTime * 1000), cancellationToken: m_refCTS.Token);

        Time.timeScale = 1.0f;
        m_bLock = false;
    }
}
