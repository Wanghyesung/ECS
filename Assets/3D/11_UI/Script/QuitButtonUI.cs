using Cysharp.Threading.Tasks;
using UnityEngine.EventSystems;

/*///////////////////////////////////////////
                QuitButtonUI
목적 : 로비 종료 버튼의 클릭음과 종료 동작을 함께 처리한다.
 *///////////////////////////////////////////
public sealed class QuitButtonUI : BaseButtonUI
{
    private bool m_bIsQuitting;

    public override void OnPointerClick(PointerEventData _tEventData)
    {
        if (m_bIsQuitting == true)
            return;

        m_bIsQuitting = true;
        base.OnPointerClick(_tEventData);
        QuitAfterClickAsync().Forget();
    }

    private async UniTaskVoid QuitAfterClickAsync()
    {
        // 앱을 같은 프레임에 닫으면 BaseButtonUI의 클릭음이 재생되기도 전에 잘린다.
        await UniTask.Delay(200, ignoreTimeScale: true,
            cancellationToken: this.GetCancellationTokenOnDestroy());
        GameSceneManager.m_Instance.QuitGame();
    }
}
