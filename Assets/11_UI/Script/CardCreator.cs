using UnityEngine;

/*///////////////////////////////////////////
                CardCreator
기능 : FeatureManager가 뽑은 후보 FeatureSO를 각 RandomFeatureCard에 분배하고,
      카드가 클릭되면 그 카드에 할당된 FeatureSO를 FeatureManager로 넘겨
      Player에게 적용시키는 중간 역할 (카드 <-> FeatureManager 라우팅 전담)
      조커카드 트리거(m_refJokerCard)도 같이 관리: 클릭 시 JokerCardManager.TryGamble 호출만 하고
      실제 도박 상태(streak, pending)는 JokerCardManager가 전담
 *///////////////////////////////////////////

public class CardCreator : MonoBehaviour
{
    [SerializeField] private RandomFeatureCard[] m_arrCard;

    [SerializeField] private RandomFeatureCard m_refJokerCard; //조커카드 전용 슬롯
    [SerializeField] private SOJokerCard m_SOJokerCard;        //조커카드 표시 데이터(아이콘 등)

    [SerializeField] private Player m_refPlayer;

    private void Awake()
    {
        for (int i = 0; i < m_arrCard.Length; ++i)
        {
            RandomFeatureCard refCard = m_arrCard[i];
            refCard.OnCardClicked += HandleCardClicked;
        }

        if (m_refJokerCard != null)
            m_refJokerCard.OnCardClicked += HandleJokerCardClicked;
    }

  
    public void ShowChoices()
    {
        // 기능 카드 UI를 제외한 나머지(몬스터 BT, 무기 쿨타임, 애니메이션 등)는
        // Time.time / Time.deltaTime 기반이라 timeScale만 0으로 만들면 별도 처리 없이 정지됨
        Time.timeScale = 0f;

        var listChoices = FeatureManager.m_Instance.RequestFeature(m_arrCard.Length);

        for (int i = 0; i < m_arrCard.Length; ++i)
            m_arrCard[i].Setup(listChoices[i]);

        if (m_refJokerCard != null)
            m_refJokerCard.Setup(m_SOJokerCard);
    }

    private void HandleCardClicked(SOData _SOData)
    {
        if (_SOData is not SOFeature SOFeature)
            return;

        FeatureManager.m_Instance.SelectFeature(SOFeature, m_refPlayer);

        Close();

        Time.timeScale = 1f;
    }

    //조커카드는 트리거일 뿐이라 payload(SOData)는 안 씀 - JokerCardManager가 자기 SO를 이미 들고 있음
    private void HandleJokerCardClicked(SOData _SOData)
    {
        if (_SOData is not SOJokerCard SOJoker)
            return;

        JokerCardManager.m_Instance.TryGamble(SOJoker);

        Close();
    }


    private void Close()
    {
        for (int i = 0; i < m_arrCard.Length; ++i)
            m_arrCard[i].gameObject.SetActive(false);

        if (m_refJokerCard != null)
            m_refJokerCard.gameObject.SetActive(false);
    }
}

