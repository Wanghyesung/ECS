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

    [Header("Reveal Animation (조커카드 결과 연출)")]
    [SerializeField] private float m_fMoveTime = 0.3f;
    [SerializeField] private Vector3 m_vRevealScale = new Vector3(1.5f, 1.5f, 1.5f);
    [SerializeField] private float m_fShakeTime = 0.6f;
    [SerializeField] private Vector2 m_vShakeStrength = new Vector2(30f, 30f);
    [SerializeField] private int m_iShakeVibrato = 20;
    [SerializeField] private float m_fShakeRandomness = 90f;

    // 조커카드 결과 연출 전체: 가운데로 이동하며 확대 -> 랜덤 흔들림 -> 성공/실패 애니메이션까지 이어서 재생 후 호출부로 제어 반환
    public async UniTask PlayResultAnimation(RectTransform _refCenterAnchor, bool _bSuccess)
    {
        RectTransform refRect = (RectTransform)transform;
        Vector2 vOriginPos = refRect.anchoredPosition;
        Vector3 vOriginScale = refRect.localScale;
        m_refImage.raycastTarget = false;

        var tSource = new UniTaskCompletionSource();

        Sequence refSeq = DOTween.Sequence();
        refSeq.Append(refRect.DOAnchorPos(_refCenterAnchor.anchoredPosition, m_fMoveTime).SetEase(Ease.OutQuad));
        refSeq.Join(refRect.DOScale(m_vRevealScale, m_fMoveTime).SetEase(Ease.OutQuad));
        refSeq.Append(refRect.DOShakeAnchorPos(m_fShakeTime, m_vShakeStrength, m_iShakeVibrato, m_fShakeRandomness));
        refSeq.SetUpdate(true); // Time.timeScale = 0(카드 UI 노출 중)에서도 재생
        refSeq.OnComplete(() => tSource.TrySetResult()); //작업이 다 완료된 시점에 콜백
        await tSource.Task; //작업이 완료되기 까지 밑에 작업 X

        if (m_refAnimTable != null)
            await m_refAnimTable.PlayAimation(_bSuccess ? eEntityState.Success : eEntityState.Fail);

        refRect.anchoredPosition = vOriginPos;
        refRect.localScale = vOriginScale;
    }
}
