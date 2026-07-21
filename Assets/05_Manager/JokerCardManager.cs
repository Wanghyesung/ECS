using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                JokerCardManager
기능 : 조커카드 도박 진행 상태(연속 성공 횟수, 아직 미확정인 보류 카드 목록)를 관리하는 매니저
      선택된 카드는 즉시 Player에 적용하지 않고 보류시켰다가, 현금화(CashOut) 시점에
      FeatureManager.SelectFeature로 일괄 적용한다.
      실패 시에는 보류 목록을 비우기만 하면 되므로(=몰수) SOFeature에 별도 Revert 로직이 필요 없다.
 *///////////////////////////////////////////

public class JokerCardManager : MonoBehaviour
{
    public static JokerCardManager m_Instance = null;

    [SerializeField] private SOJokerCard m_SOConfig;

    private int m_iLevel = 0;
    public int Streak => m_iLevel;

    private List<SOFeature> m_listPendingFeature = new List<SOFeature>();
    public IReadOnlyList<SOFeature> PendingFeature => m_listPendingFeature;

    // 현재 내가 적용한 기능들
    private List<SOFeature> m_listApplyFeature = new List<SOFeature>();

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(this);

        m_Instance = this;
    }

    // 도박 1회 시도. 성공하면 이번 회차의 후보 목록을 반환하고, 실패하면 보류 목록을 몰수(초기화)한다.
    public bool TryGamble(out List<SOFeature> _outCandidates)
    {
        bool bSuccess = Random.value < m_SOConfig.GetSuccessChance(m_iLevel);

        if (!bSuccess)
        {
            m_listPendingFeature.Clear();
            m_iLevel = 0;
            _outCandidates = null;
            return false;
        }

        m_iLevel++;

        var listResult = FeatureManager.m_Instance.RequestFeature(m_SOConfig.GetCandidateCount(m_iLevel));


        m_listPendingFeature.AddRange(listResult);
        _outCandidates = m_listPendingFeature;
        return true;
    }

    public int GetCurrentPickCount() => m_SOConfig.GetPickCount(m_iLevel);

    // 후보 중 고른 카드들을 보류 목록에 추가만 함 (Player에는 아직 미적용)
    public void ConfirmPicks(List<SOFeature> _listPicked)
    {
        m_listPendingFeature.AddRange(_listPicked);
    }

    public void CashOut(Player _refPlayer)
    {
        for (int i = 0; i < m_listPendingFeature.Count; ++i)
            FeatureManager.m_Instance.SelectFeature(m_listPendingFeature[i], _refPlayer);

        m_listPendingFeature.Clear();
        m_iLevel = 0;
    }
}
