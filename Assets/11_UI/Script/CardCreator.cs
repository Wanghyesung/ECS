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
        // 기능 카드 UI를 제외한 나머지(몬스터 BT, 무기 쿨타임, 애니메이션 등)는
        // Time.time / Time.deltaTime 기반이라 timeScale만 0으로 만들면 별도 처리 없이 정지됨
        Time.timeScale = 0f;

        var listChoices = FeatureManager.m_Instance.RequestFeatureChoices(m_arrCard.Length);

        for (int i = 0; i < m_arrCard.Length; ++i)
            m_arrCard[i].Setup(listChoices[i]);
    }

    private void HandleCardClicked(SOFeature _SOFeature)
    {
        FeatureManager.m_Instance.SelectFeature(_SOFeature, m_refPlayer);

        for (int i = 0; i < m_arrCard.Length; ++i)
            m_arrCard[i].gameObject.SetActive(false);

        Time.timeScale = 1f;
    }
}
