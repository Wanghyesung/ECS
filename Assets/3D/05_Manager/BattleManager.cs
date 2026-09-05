using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Threading;
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

    private CancellationTokenSource m_refCancell;
    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

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

    private void AddExp(int _iAmount)
    {
        if (_iAmount <= 0)
            return;

        m_iCurrentExp += _iAmount;

        while (m_iCurrentExp >= m_iMaxExp)
        {
            m_iCurrentExp -= m_iMaxExp;
            ++m_iPendingLevelUps;
        }

        // 이전 작업이 진행 중이라면 Cancel 및 Dispose (StopCoroutine 역할)
        if (m_refCancell != null)
        {
            m_refCancell.Cancel();
            m_refCancell.Dispose();
        }

        // Unity Destroy 토큰과 연동된 새 CTS 생성
        m_refCancell = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

        LevelUPAsync(m_refCancell.Token).Forget();
    }

    private async UniTaskVoid LevelUPAsync(CancellationToken _tToken)
    {
       
        while (m_iPendingLevelUps > 0)
        {
            int iCountBefore = m_iPendingLevelUps;

            OnExpChanged?.Invoke(m_iMaxExp, m_iMaxExp);

            // ExSlider가 Max까지 다 차서 LevelUp()이 호출되어 보류 개수가 줄어들 때까지 대기
            await UniTask.WaitUntil(() => m_iPendingLevelUps < iCountBefore, cancellationToken: _tToken);
        }

        // 보류된 레벨업을 모두 처리했으면 실제 잔여 경험치로 슬라이더를 되돌림
        OnExpChanged?.Invoke(m_iCurrentExp, m_iMaxExp);
     
        // 작업 정상 종료 시 CTS 정리
        if (m_refCancell != null && m_refCancell.Token == _tToken)
        {
            m_refCancell.Dispose();
            m_refCancell = null;
        }
        
    }
}
