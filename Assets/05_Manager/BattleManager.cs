using System;
using System.Collections;
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
    private int m_iPendingLevelUps = 0; // ExSlider가 다 찰 때까지 미뤄둔 레벨업 개수

    public int CurrentExp => m_iCurrentExp;
    public int MaxExp => m_iMaxExp;
    public int CurrentLevel => m_iCurrentLevel;

    public event Action<int, int> OnExpChanged; // (현재 Exp, Max Exp)
    public event Action<int> OnLevelUp;         // (새 레벨)

    private Coroutine m_COLevelUp = null;
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
            ++m_iPendingLevelUps;
        }

        // 이미 순차 처리 중이면 m_iPendingLevelUps만 늘려두고 코루틴은 그대로 두면
        // 진행 중인 while 루프가 알아서 늘어난 만큼 이어서 처리함 (중복 실행 방지)
        if (m_COLevelUp == null)
            m_COLevelUp = StartCoroutine(CoLevelUP());
    }

    // ExSlider의 채우기 연출이 실제로 Max에 도달했을 때 Player가 호출
    public void LevelUp()
    {
        if (m_iPendingLevelUps <= 0)
            return;

        m_iCurrentLevel += 1;
        m_iPendingLevelUps -= 1;

        OnLevelUp?.Invoke(m_iCurrentLevel);
        m_refCardCreator?.ShowChoices();
    }


    // m_iPendingLevelUps 만큼 ExSlider를 Max까지 채우는 연출을 한 레벨씩 순차 재생
    // (한 번에 10레벨을 올려도 카드가 10번 순서대로 뜨도록)
    private IEnumerator CoLevelUP()
    {
        while (m_iPendingLevelUps > 0)
        {
            int iCountBefore = m_iPendingLevelUps;

            OnExpChanged?.Invoke(m_iMaxExp, m_iMaxExp);

            // ExSlider가 Max까지 다 차서 LevelUp()이 호출되어 보류 개수가 줄어들 때까지 대기
            yield return new WaitUntil(() => m_iPendingLevelUps < iCountBefore);
        }

        // 보류된 레벨업을 모두 처리했으면 실제 잔여 경험치로 슬라이더를 되돌림
        OnExpChanged?.Invoke(m_iCurrentExp, m_iMaxExp);
        m_COLevelUp = null;
    }
}
