// RandomFeatureCard.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


/*///////////////////////////////////////////
                RandomFeatureCard
기능 : CardCreator가 전달한 SOFeat을 바탕으로 카드 이미지 설정,
      해당 카드를 클릭시 OnCardClicked를 통해 구독한 이벤트에게 내 SOFeat전달
 *///////////////////////////////////////////

public class RandomFeatureCard : BaseButtonUI
{
    [SerializeField] private Sprite m_refTargetSprite = null;
    [SerializeField] private Sprite m_refOriginSprite = null; //현재 보여주는 이미지
    [SerializeField] private Image m_refImage = null;

    [SerializeField] private float m_fShowTime = 2.0f;
    private SOFeature m_SOFeature = null;
    public SOFeature SOFeature => m_SOFeature;

    public event Action<SOFeature> OnCardClicked;

    private Coroutine m_CORotate = null;

    private void OnEnable()
    {
        m_refImage.sprite = m_refOriginSprite;
        m_refImage.raycastTarget = false;

        m_CORotate = StartCoroutine(CORotate());
    }
    
    public void Setup(SOFeature _refFeature)
    {
        m_SOFeature = _refFeature;

        m_refTargetSprite = _refFeature.Icon;
        gameObject.SetActive(true);
    }

    override public void OnPointerClick(PointerEventData e)
    {
        base.OnPointerClick(e);
        OnCardClicked?.Invoke(m_SOFeature);
     
    }
 

    
    private IEnumerator CORotate()
    {
        // 레벨업 시 Time.timeScale이 0이 되어도 카드 연출은 계속 움직여야 하므로 unscaledDeltaTime 사용
        float fElapsed = 0.0f;
        while (fElapsed < m_fShowTime)
        {
            transform.Rotate(Vector3.up * 720f * Time.unscaledDeltaTime);
            fElapsed += Time.unscaledDeltaTime;

            yield return null;
        }

        transform.Rotate(Vector3.zero);

        m_refImage.sprite = m_refTargetSprite;
        m_refImage.raycastTarget = true;
        m_CORotate = null;
    }
}