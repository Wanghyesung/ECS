using DG.Tweening;
using R3;
using UnityEngine;
using UnityEngine.UI;

/*///////////////////////////////////////////
                BossWarningView
목적 : 보스 등장 컷신(DungeonManager.IsBossCutscene) 동안에만 Warning 이미지를 깜빡이고,
       컷신이 끝나면 숨긴다. 
 *///////////////////////////////////////////
[RequireComponent(typeof(Image))]
public sealed class BossWarningView : MonoBehaviour
{
    [SerializeField] private float m_fBlinkHalfTime = 0.3f;   // 켜짐(또는 꺼짐)까지 걸리는 시간

    private Image m_refImage;
    private Tween m_refBlinkTween;

    // DungeonManager 는 LobyScene 에서 넘어온 DDOL 이라 BattleScene 의 Awake 시점엔 이미 존재한다
    private void Awake()
    {
        m_refImage = GetComponent<Image>();
        SetAlpha(0f);

        DungeonManager.m_Instance.IsBossCutscene.Subscribe(OnBossCutsceneChanged).AddTo(this);
    }

    private void OnDestroy()
    {
        m_refBlinkTween?.Kill();
    }

    private void OnBossCutsceneChanged(bool _bIsCutscene)
    {
        m_refBlinkTween?.Kill();

        if (_bIsCutscene == false)
        {
            SetAlpha(0f);
            return;
        }

        // 컷신 동안 timeScale 이 0 이라 unscaled 로 돌려야 깜빡인다
        SetAlpha(1f);
        m_refBlinkTween = m_refImage.DOFade(0f, m_fBlinkHalfTime)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);
    }

    private void SetAlpha(float _fAlpha)
    {
        Color tColor = m_refImage.color;
        tColor.a = _fAlpha;
        m_refImage.color = tColor;
    }
}
