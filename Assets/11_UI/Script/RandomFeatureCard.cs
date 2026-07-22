// RandomFeatureCard.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


/*///////////////////////////////////////////
                RandomFeatureCard
기능 : CardCreator가 전달한 SOData를 바탕으로 카드 이미지 설정,
      해당 카드를 클릭시 OnCardClicked를 통해 구독한 이벤트에게 내 SOData전달
      (SOFeature 전용이 아니라 Icon만 쓰므로 SOData 아무거나 받을 수 있음 - 기능카드/조커카드 공용)
 *///////////////////////////////////////////

public class RandomFeatureCard : BaseButtonUI
{
    [SerializeField] private Sprite m_refTargetSprite = null;
    [SerializeField] private Sprite m_refOriginSprite = null; //현재 보여주는 이미지
    [SerializeField] private Image m_refImage = null;

    [SerializeField] private float m_fShowTime = 2.0f;
    private SOData m_SOData = null;
    public SOData Data => m_SOData;

    public event Action<SOData> OnCardClicked;

    private Coroutine m_CORotate = null;

    private void OnEnable()
    {
        m_refImage.sprite = m_refOriginSprite;
        m_refImage.raycastTarget = false;

        m_CORotate = StartCoroutine(CORotate());
    }
    
    public void Setup(SOData _refData)
    {
        m_SOData = _refData;

        m_refTargetSprite = _refData.Icon;
        gameObject.SetActive(true);
    }

    public override void OnPointerClick(PointerEventData _eventData)
    {
        base.OnPointerClick(_eventData);
        OnCardClicked?.Invoke(m_SOData);
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