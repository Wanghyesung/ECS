// JokerFeatureCard.cs
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/*///////////////////////////////////////////
                JokerFeatureCard
기능 : 조커카드 전용 결과 연출(가운데 이동+확대 -> 흔들림 -> 성공/실패 애니메이션) 담당.
      클릭/데이터 로직은 RandomFeatureCard와 동일해서 상속만 받고, 연출 관련 필드/로직만 추가.
 *///////////////////////////////////////////

public class JokerFeatureCard : RandomFeatureCard
{
    [SerializeField] private AnimationTable m_refAnimTable = null;
    [Header("Audio")]
    [SerializeField] private SOAudio m_SOShakeAudio;
    [SerializeField] private SOAudio m_SOSuccessAudio;
    [SerializeField] private SOAudio m_SOFailAudio;

    [Header("Reveal Animation (조커카드 결과 연출)")]
    [SerializeField] private float m_fMoveTime = 0.3f;
    [SerializeField] private Vector3 m_vRevealScale = new Vector3(1.5f, 1.5f, 1.5f);
    [SerializeField] private float m_fShakeTime = 0.6f;
    [SerializeField] private Vector2 m_vShakeStrength = new Vector2(30f, 30f);
    [SerializeField] private int m_iShakeVibrato = 20;
    [SerializeField] private float m_fShakeRandomness = 90f;

    private SoundManager.SoundHandle m_tShakeHandle;
    private Sequence m_refResultSequence;

    private void OnDisable()
    {
        m_refResultSequence?.Kill();
        StopShakeAudio();
    }

    private void OnDestroy() => m_refResultSequence?.Kill();

    // 조커카드 결과 연출 전체: 가운데로 이동하며 확대 -> 랜덤 흔들림 -> 성공/실패 애니메이션까지 이어서 재생 후 호출부로 제어 반환
    public async UniTask PlayResultAnimation(RectTransform _refCenterAnchor, bool _bSuccess)
    {
        RectTransform refRect = (RectTransform)transform;
        Vector2 vOriginPos = refRect.anchoredPosition;
        Vector3 vOriginScale = refRect.localScale;
        m_refImage.raycastTarget = false;

        var tSource = new UniTaskCompletionSource();

        Sequence refSeq = DOTween.Sequence();
        m_refResultSequence = refSeq;
        refSeq.Append(refRect.DOAnchorPos(_refCenterAnchor.anchoredPosition, m_fMoveTime).SetEase(Ease.OutQuad));
        refSeq.Join(refRect.DOScale(m_vRevealScale, m_fMoveTime).SetEase(Ease.OutQuad));
        refSeq.AppendCallback(StartShakeAudio);
        refSeq.Append(refRect.DOShakeAnchorPos(m_fShakeTime, m_vShakeStrength, m_iShakeVibrato, m_fShakeRandomness));
        refSeq.AppendCallback(StopShakeAudio);
        refSeq.SetUpdate(true); // Time.timeScale = 0(카드 UI 노출 중)에서도 재생
        refSeq.OnComplete(() => tSource.TrySetResult()); //작업이 다 완료된 시점에 콜백
        refSeq.OnKill(() =>
        {
            StopShakeAudio();
            m_refResultSequence = null;
            tSource.TrySetCanceled();
        });
     
        await tSource.Task; //작업이 완료되기 까지 밑에 작업 X

        SoundManager.m_Instance.PlaySfx(_bSuccess == true ? m_SOSuccessAudio : m_SOFailAudio);

        if (m_refAnimTable != null)
            await m_refAnimTable.PlayAimation(_bSuccess ? eEntityState.Success : eEntityState.Fail);
        
         refRect.anchoredPosition = vOriginPos;
         refRect.localScale = vOriginScale;
    }

    private void StartShakeAudio()
    {
        StopShakeAudio();
        m_tShakeHandle = SoundManager.m_Instance.PlaySfx(m_SOShakeAudio);
    }

    private void StopShakeAudio()
    {
        if (m_tShakeHandle.IsValid == false)
            return;

        if (SoundManager.m_Instance != null)
            SoundManager.m_Instance.StopSfx(m_tShakeHandle);
        m_tShakeHandle = default;
    }
}
