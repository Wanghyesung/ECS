using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/*///////////////////////////////////////////
                CardCreator
기능 : FeatureManager가 뽑은 후보 FeatureSO를 각 RandomFeatureCard에 분배하고,
      카드가 클릭되면 그 카드에 할당된 FeatureSO를 FeatureManager로 넘겨
      Player에게 적용시키는 중간 역할 (카드 <-> FeatureManager 라우팅 전담)
      조커카드 트리거(m_refJokerCard)도 같이 관리: 클릭 시 RollGamble로 판정 후 연출을 재생
 *//////////////////////////////////////////

public class CardCreator : MonoBehaviour
{
    [SerializeField] private RandomFeatureCard[] m_arrCard;

    [SerializeField] private JokerFeatureCard m_refJokerCard; //조커카드 전용 슬롯
    [SerializeField] private SOJokerCard m_SOJokerCard;        //조커카드 표시 데이터(아이콘 등)
    [SerializeField] private RectTransform m_refCenterAnchor; //결과 연출 시 이동할 화면 중앙 위치

    [SerializeField] private TextMeshProUGUI m_refJokerSuccessText;
    [SerializeField] private TextMeshProUGUI m_refCurrentLevel;
    //[SerializeField] private 
    private void Start()
    {
        for (int i = 0; i < m_arrCard.Length; ++i)
        {
            RandomFeatureCard refCard = m_arrCard[i];
            refCard.OnCardClicked += HandleCardClicked;
        }

        if (m_refJokerCard != null)
            m_refJokerCard.OnCardClicked += HandleJokerCardClicked;
    }

    private void OnEnable()
    {
        float fJokerSuccessValue = JokerCardManager.m_Instance.GetSuccessValue();
        float fJokerLevelValue = JokerCardManager.m_Instance.Level;

        int iJokerSuccessValue = (int)(fJokerSuccessValue * 100.0f);
        m_refJokerSuccessText.text = iJokerSuccessValue.ToString();
        m_refCurrentLevel.text = fJokerLevelValue.ToString();
    }

    public void ShowChoices()
    {
        // 기능 카드 UI를 제외한 나머지(몬스터 BT, 무기 쿨타임, 애니메이션 등)는
        // Time.time / Time.deltaTime 기반이라 timeScale만 0으로 만들면 별도 처리 없이 정지됨
        gameObject.SetActive(true);

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

        FeatureManager.m_Instance.SelectFeature(SOFeature, Player.CurrentPlayer);

        Close();

        Time.timeScale = 1f;
    }

    //조커카드는 트리거일 뿐이라 payload(SOData)는 안 씀 - JokerCardManager가 자기 SO를 이미 들고 있음
    //async void는 C# 표준 SynchronizationContext를 타고 들어가며 힙 메모리 할당(GC Alloc)을 만듭니다. 반면 UniTaskVoid는 Zero-Allocation으로 동작
    private async void HandleJokerCardClicked(SOData _SOData)
    {
        if (_SOData is not SOJokerCard SOJoker)
            return;

        //연출(성공/실패 애니메이션)에 결과가 필요하므로 애니메이션 재생 전에 판정부터 수행
        bool bSuccess = JokerCardManager.m_Instance.RollGamble(SOJoker);

        await m_refJokerCard.PlayResultAnimation(m_refCenterAnchor, bSuccess);

        if (bSuccess == true)
            JokerCardManager.m_Instance.ApplySuccess();
        else
            JokerCardManager.m_Instance.ApplyFail();

        Close();
    }


    private void Close()
    {
        gameObject.SetActive(false);
    }
}

