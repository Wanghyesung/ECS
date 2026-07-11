using UnityEngine;

/*///////////////////////////////////////////
                CardCreator
기능 : FeatureManager가 뽑은 후보 FeatureSO를 각 RandomFeatureCard에 분배하고,
      카드가 클릭되면 그 카드에 할당된 FeatureSO를 FeatureManager로 넘겨
      Player에게 적용시키는 중간 역할 (카드 <-> FeatureManager 라우팅 전담)
 *///////////////////////////////////////////

public class CardCreator : MonoBehaviour
{
    [SerializeField] private RandomFeatureCard[] m_arrCard;
    [SerializeField] private Player m_refPlayer;

    private void Awake()
    {
        for (int i = 0; i < m_arrCard.Length; ++i)
        {
            RandomFeatureCard refCard = m_arrCard[i];
            refCard.OnCardClicked += HandleCardClicked;
        }
    }

    public void ShowChoices()
    {
        for (int i = 0; i < m_arrCard.Length; ++i)
        {
            var SOFeat = FeatureManager.m_Instance.RequestFeatureChoice();
            m_arrCard[i].Setup(SOFeat);
        }
    }

    private void HandleCardClicked(SOFeature _SOFeature)
    {
        FeatureManager.m_Instance.SelectFeature(_SOFeature, m_refPlayer);

        for (int i = 0; i < m_arrCard.Length; ++i)
            m_arrCard[i].gameObject.SetActive(false);
    }
}
