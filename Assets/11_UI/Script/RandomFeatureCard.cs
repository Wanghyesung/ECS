// RandomFeatureCard.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
        m_refImage.raycastTarget = false;

        m_CORotate = StartCoroutine(CORotate());
    }

    public void Setup(SOFeature _refFeature)
    {
        m_SOFeature = _refFeature;

        m_refTargetSprite = _refFeature.Icon;
        gameObject.SetActive(true);
    }

    protected override void OnClicked()
    {
        OnCardClicked?.Invoke(m_SOFeature);
    }

    protected override void OnPressed()
    {
        // 눌림 연출 트리거 (Animator 등, 매 프레임 로직 없음)
    }

    protected override void OnReleased()
    {
        // 복원 연출 트리거
    }


    private IEnumerator CORotate()
    {
        float fElapsed = 0.0f;
        while (fElapsed < m_fShowTime)
        {
            transform.Rotate(Vector3.up * 90f * Time.deltaTime);
            fElapsed += Time.deltaTime;

            yield return null; 
        }

        m_refImage.raycastTarget = true;
        m_CORotate = null;
    }
}