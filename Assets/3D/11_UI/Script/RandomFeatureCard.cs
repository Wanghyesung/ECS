// RandomFeatureCard.cs
using Cysharp.Threading.Tasks;
using R3;
using System.Collections;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


/*///////////////////////////////////////////
                RandomFeatureCard
기능 : CardCreator가 전달한 SOData를 바탕으로 카드 이미지 설정,
      해당 카드를 클릭시 OnCardClicked를 통해 구독한 이벤트에게 내 SOData전달
      (SOFeature 전용이 아니라 Icon만 쓰므로 SOData 아무거나 받을 수 있음 - 기능카드/조커카드 공용)
 *///////////////////////////////////////////

public class RandomFeatureCard : BaseButtonUI, ITooltipDataable
{
    [SerializeField] private Sprite m_refTargetSprite = null;
    [SerializeField] private Sprite m_refOriginSprite = null; //현재 보여주는 이미지
    [SerializeField] protected Image m_refImage = null;
    [SerializeField] protected Image m_refSlotImage = null;

    private const float ROTATE_SPEED = 720.0f;   //초당 회전 각도

    [SerializeField] private float m_fShowTime = 2.0f;
    private SOData m_SOData = null;
    public SOData Data => m_SOData;

    private SODataTooltipTrigger m_refTooltip = null;
    private bool m_bRotate = false;                  //회전 연출이 끝나 앞면이 보이는 상태인지
    public SOData TooltipData => m_bRotate == false ? null : m_SOData;   //뒷면이면 후보가 미리 새지 않게 null

    private readonly Subject<SOData> m_subjectCardClick = new();
    public Observable<SOData> OnCardClicked => m_subjectCardClick;
    private readonly Subject<Unit> m_subjectRotationCompleted = new();
    public Observable<Unit> OnRotationCompleted => m_subjectRotationCompleted;

    private void Awake()
    {
        m_refSlotImage = GetComponent<Image>();
        m_refTooltip = GetComponent<SODataTooltipTrigger>();
    }

    private void OnEnable()
    {
        m_refSlotImage.color = Color.white;
    }

    public void Setup(SOData _SOData)
    {
        m_SOData = _SOData;
        m_bRotate = false;

        m_refTargetSprite = _SOData.Icon;
        gameObject.SetActive(true);

        m_refImage.sprite = m_refOriginSprite;
        m_refImage.raycastTarget = false;

        RotateAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    public override void OnPointerClick(PointerEventData _eventData)
    {
        //회전 연출 중에는 아직 뒷면이므로 클릭을 무시한다 (루트 Image 가 raycast 를 받아 연출 중에도 눌리던 문제)
        if (m_bRotate == false)
            return;

        base.OnPointerClick(_eventData);
        m_subjectCardClick.OnNext(m_SOData);
    }

    private async UniTaskVoid RotateAsync(System.Threading.CancellationToken _tToken)
    {
        //프레임마다 Rotate 로 더하면 마지막 프레임 오버슈트(720 x dt)만큼 각이 남아 정면에서 벗어나고,
        //그 잔여각이 레벨업마다 쌓인다 - 진행률로 절대 각도를 지정하고 360 배수로 끝내 항상 정면에서 멈춘다
        float fTotalAngle = Mathf.Round(ROTATE_SPEED * m_fShowTime / 360.0f) * 360.0f;

        // 레벨업 시 Time.timeScale이 0이 되어도 카드 연출은 계속 움직여야 하므로 unscaledDeltaTime 사용
        float fElapsed = 0.0f;
        while (fElapsed < m_fShowTime)
        {
            fElapsed += Time.unscaledDeltaTime;

            float fRatio = Mathf.Clamp01(fElapsed / m_fShowTime);
            transform.localRotation = Quaternion.Euler(0.0f, fTotalAngle * fRatio, 0.0f);

            await UniTask.Yield(_tToken);
        }

        transform.localRotation = Quaternion.identity;

        m_refImage.sprite = m_refTargetSprite;
        m_refImage.raycastTarget = true;

        m_refSlotImage.color = m_SOData is SOFeature refFeature ? FeatureTierUI.GetColor(refFeature.Tier) : Color.white;

        //연출 중에 이미 마우스가 올라와 있었으면 Enter 가 다시 오지 않으므로 여기서 띄운다
        m_bRotate = true;
        if (m_refTooltip != null)
            m_refTooltip.Refresh();

        m_subjectRotationCompleted.OnNext(Unit.Default);
    }
}
