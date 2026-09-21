using UnityEngine;
using UnityEngine.UI;

/*///////////////////////////////////////////
                SODataTooltipView
목적 : SOData 를 가진 UI(SlotView / RandomFeatureCard) 위에 마우스가 올라가면
       아이콘 + Description 을 그 옆에 띄우는 공용 설명창.
       씬마다 하나, BaseButtonUI 가 static Show/Hide 로 호출. Update 없음.
 *///////////////////////////////////////////
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(DataDescUI))]
public sealed class SODataTooltipView : MonoBehaviour
{
    [SerializeField] private float m_fOffset = 8f;
    [SerializeField] private float m_fScreenMargin = 8f;

    private static SODataTooltipView m_Instance = null;

    private RectTransform m_refRect;
    private CanvasGroup m_refCanvasGroup;
    private DataDescUI m_refDescView;

    private RectTransform m_refCanvasRect;
    private Camera m_refCanvasCamera;          //Overlay 캔버스면 null

    private readonly Vector3[] m_arrCornerBuffer = new Vector3[4];

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this) { Destroy(gameObject); return; }
        m_Instance = this;

        m_refRect = (RectTransform)transform;
        m_refCanvasGroup = GetComponent<CanvasGroup>();
        m_refDescView = GetComponent<DataDescUI>();

        Canvas refCanvas = GetComponentInParent<Canvas>().rootCanvas;
        m_refCanvasRect = (RectTransform)refCanvas.transform;
        m_refCanvasCamera = refCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : refCanvas.worldCamera;

        m_refRect.pivot = new Vector2(0f, 1f);
        m_refCanvasGroup.blocksRaycasts = false;   //창이 커서 밑에 깔려도 슬롯의 Enter/Exit 를 가로채지 않게 (깜빡임 방지)
        m_refCanvasGroup.interactable = false;
        HideInternal();
    }

    private void OnDestroy()
    {
        if (m_Instance == this)
            m_Instance = null;
    }

    public static void Show(BaseButtonUI _refOwner, SOData _refData, RectTransform _refSource, Camera _refSourceCamera)
    {
        if (m_Instance == null || _refData == null)
            return;

        m_Instance.m_refDescView.Show(_refData);
        m_Instance.Place(_refSource, _refSourceCamera);
        m_Instance.m_refCanvasGroup.alpha = 1f;
    }

    public static void Hide(BaseButtonUI _refOwner)
    {
        if (m_Instance == null)
            return;

        m_Instance.HideInternal();
    }

    private void HideInternal()
    {
        m_refCanvasGroup.alpha = 0f;
    }

    //대상 오른쪽에 윗변 맞춤. 오른쪽이 모자라면 왼쪽으로 뒤집고 Canvas 안으로 clamp
    private void Place(RectTransform _refSource, Camera _refSourceCamera)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_refRect);

        //대상과 이 창의 Canvas 가 달라도 맞도록 화면 좌표를 거쳐 Canvas 로컬로 변환
        _refSource.GetWorldCorners(m_arrCornerBuffer);
        Vector2 vSrcMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 vSrcMax = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < m_arrCornerBuffer.Length; ++i)
        {
            Vector2 vScreen = RectTransformUtility.WorldToScreenPoint(_refSourceCamera, m_arrCornerBuffer[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_refCanvasRect, vScreen, m_refCanvasCamera, out Vector2 vLocal);
            vSrcMin = Vector2.Min(vSrcMin, vLocal);
            vSrcMax = Vector2.Max(vSrcMax, vLocal);
        }

        Rect tCanvas = m_refCanvasRect.rect;
        Vector2 vSize = m_refRect.rect.size;

        float fX = vSrcMax.x + m_fOffset;
        if (fX + vSize.x > tCanvas.xMax - m_fScreenMargin)
            fX = vSrcMin.x - m_fOffset - vSize.x;

        fX = Mathf.Clamp(fX, tCanvas.xMin + m_fScreenMargin, tCanvas.xMax - m_fScreenMargin - vSize.x);
        float fY = Mathf.Clamp(vSrcMax.y, tCanvas.yMin + m_fScreenMargin + vSize.y, tCanvas.yMax - m_fScreenMargin);

        m_refRect.position = m_refCanvasRect.TransformPoint(new Vector3(fX, fY, 0f));
    }
}
