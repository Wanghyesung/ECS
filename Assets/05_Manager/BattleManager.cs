using System;
using UnityEngine;

/*///////////////////////////////////////////
                BattleManager
기능 : 몬스터 처치 시 EXP를 누적하고, Max 도달 시 레벨업 처리 후
      CardCreator를 통해 FeatureManager의 랜덤 기능 카드를 노출시키는 기능
      HP는 Player/Monster가 각자 소유(IDamageable 패턴)하지만,
      EXP/레벨은 엔티티 상태가 아닌 '전투 진행 지표'라 이 매니저가 전담
 *///////////////////////////////////////////
public class BattleManager : MonoBehaviour
{
    public static BattleManager m_Instance = null;

    [SerializeField] private int m_iMaxExp = 100;
    [SerializeField] private CardCreator m_refCardCreator = null;

    private int m_iCurrentExp = 0;
    private int m_iCurrentLevel = 1;

    public int CurrentExp => m_iCurrentExp;
    public int MaxExp => m_iMaxExp;
    public int CurrentLevel => m_iCurrentLevel;

    public event Action<int, int> OnExpChanged; // (현재 Exp, Max Exp)
    public event Action<int> OnLevelUp;         // (새 레벨)

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(this);

        m_Instance = this;
        DontDestroyOnLoad(this);
    }

    private void OnEnable()
    {
        Monster.OnMonsterDied += HandleMonsterDied;
    }

    private void OnDisable()
    {
        Monster.OnMonsterDied -= HandleMonsterDied;
    }

    private void HandleMonsterDied(int _iAmount)
    {
        AddExp(_iAmount);
    }

    private void AddExp(int _iAmount)
    {
        if (_iAmount <= 0)
            return;

        m_iCurrentExp += _iAmount;

        // 한 번에 여러 레벨을 넘길 수도 있어 while로 처리 (초과분 이월)
        while (m_iCurrentExp >= m_iMaxExp)
        {
            m_iCurrentExp -= m_iMaxExp;
            LevelUp();
        }

        OnExpChanged?.Invoke(m_iCurrentExp, m_iMaxExp);
    }

    private void LevelUp()
    {
        ++m_iCurrentLevel;
        OnLevelUp?.Invoke(m_iCurrentLevel);

        m_refCardCreator?.ShowChoices();
    }
}
