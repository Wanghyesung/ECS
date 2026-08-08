using UnityEngine;

/*///////////////////////////////////////////
                SOJokerCard
기능 : 조커카드 도박 시스템의 회차별 성공 확률 / 후보 카드 수 / 선택 가능 수 곡선을 담는 데이터 SO
      런타임 상태(현재 연속 성공 횟수 등)는 이 SO가 아닌 JokerCardManager가 보유 (SO 데이터 오염 방지)
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SOJokerCard", menuName = "Game/Feature/SOJokerCard")]
public class SOJokerCard : SOData
{
    public override eDataType DataType { get => eDataType.Features;}

    public override int SubDataType => -1;

    [Tooltip("x = 현재까지 연속 성공 횟수, y = 다음 도박 성공 확률(0~1)")]
    [SerializeField] private AnimationCurve m_refSuccessChance;

    [Tooltip("x = 연속 성공 횟수, y = 그 회차에 보여줄 후보 카드 수")]
    [SerializeField] private AnimationCurve m_refCandidateCount;

    [Tooltip("x = 연속 성공 횟수, y = 그 중 고를 수 있는 카드 수")]
    [SerializeField] private AnimationCurve m_refPickCount;

    [Tooltip("x = 연속 성공 횟수, y = 실패 시 잃을 카드 수")]
    [SerializeField] private AnimationCurve m_refLostCardCoun;

    public float GetSuccessValue(int _iLevel) => Mathf.Clamp01(m_refSuccessChance.Evaluate(_iLevel));
    public int GetCandidateCount(int _iLevel) => Mathf.Max(0, Mathf.RoundToInt(m_refCandidateCount.Evaluate(_iLevel)));
    public int GetPickCount(int _iLevel) => Mathf.Max(0, Mathf.RoundToInt(m_refPickCount.Evaluate(_iLevel)));
    public int GetLostCount(int _iLevel) => Mathf.Max(0, Mathf.RoundToInt(m_refLostCardCoun.Evaluate(_iLevel)));
}
