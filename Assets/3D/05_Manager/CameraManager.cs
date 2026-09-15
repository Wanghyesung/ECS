using Cinemachine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using static Unity.Collections.AllocatorManager;

public class CameraManager : MonoBehaviour
{
    public static CameraManager m_Instance = null;

    [SerializeField] private Camera m_refMainCamera;
    [SerializeField] private Transform m_refPlayer;
    [SerializeField] private Vector3 m_vOffset = new Vector3(0.0f, 5.0f, -10.0f);

    [SerializeField] private Image m_refBloodScreen = null;
    private Color m_tBloodColor = Color.white;

    private Vector3 m_vShakeOffset = Vector3.zero;
    private bool m_bLock = false;
    // 컷신(MoveToPoint/FollowTarget) 진행 중 여부. 다른 연출(보스 등장 등)이 겹치지 않게 대기하는 데 사용
    public bool IsLocked => m_bLock;

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
    private void OnDestroy()
    {
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
        ShakeRoutine(this.GetCancellationTokenOnDestroy(), _fMagnitude, _fDuration).Forget();
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
    public async UniTask MoveToPoint(CancellationToken _tToken, Vector3 _vPosition, Quaternion _qLookRot, float _fMoveTime = 0.0f, float _fWaitTime = 0.0f)
    {
        m_bLock = true;
        Transform refCamTransform = m_refMainCamera.transform;

        var refSource = new UniTaskCompletionSource();
        using (_tToken.Register(() => refSource.TrySetCanceled(_tToken)))
        {
            Sequence refSeq = DOTween.Sequence();
            refSeq.Append(refCamTransform.DOMove(_vPosition, _fMoveTime).SetEase(Ease.OutQuad));
            refSeq.Join(refCamTransform.DORotateQuaternion(_qLookRot, _fMoveTime).SetEase(Ease.OutQuad));
            refSeq.SetUpdate(true);

            refSeq.OnComplete(() =>
            {
                refSource.TrySetResult();
            });

            refSeq.OnKill(() =>
            {
                refSource.TrySetCanceled();
            });
            await refSource.Task;
        }

        if (_fWaitTime > 0.0f)
            await UniTask.Delay((int)(_fWaitTime * 1000), ignoreTimeScale: true, cancellationToken: _tToken);

        Time.timeScale = 1.0f;
        m_bLock = false;
    }

    // 지정 시간 동안 타겟을 타겟 로컬 오프셋(z- = 뒤, y+ = 위) 위치에서 바라보며 따라간다
    // (unscaled - 컷신 중 timeScale 0 전제). 타겟이 어느 방향으로 날아가든 항상 "뒤 위에서" 보게 됨.
    // 끝나도 잠금은 풀지 않는다 - 이어서 MoveToPoint로 빠져나오는 연출(핵폭탄: 미사일 추적 → 맵 전경 후진)을
    // 붙이기 위함. 잠금 해제/timeScale 복귀는 MoveToPoint 쪽이 담당
    public async UniTask FollowTarget(CancellationToken _tToken, Transform _refTarget, Vector3 _vLocalOffset, float _fDuration)
    {
        m_bLock = true;
        Transform refCamTransform = m_refMainCamera.transform;

        float fElapsed = 0f;
        while (fElapsed < _fDuration && _refTarget != null)
        {
            Vector3 vTargetPos = _refTarget.position;
            refCamTransform.position = vTargetPos + _refTarget.rotation * _vLocalOffset;
            refCamTransform.rotation = Quaternion.LookRotation(vTargetPos - refCamTransform.position);

            fElapsed += Time.unscaledDeltaTime;
            // 타겟(미사일) 이동이 끝난 뒤 위치를 잡아야 한 프레임 늦게 따라가는 떨림이 없음
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, _tToken);
        }
    }
}
