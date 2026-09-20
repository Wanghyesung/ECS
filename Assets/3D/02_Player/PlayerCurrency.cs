using R3;

/*///////////////////////////////////////////
                PlayerCurrency
목적 : 스탯 강화 등에 소모되는 재화의 임시 보관소. 정식 이코노미/세이브 시스템이
      들어오기 전까지 프로세스 생존 기간 동안만 유지되는 static 카운터.
      PlayerPreLoadData와 마찬가지로 재시작하면 초기화된다.
 *///////////////////////////////////////////

public static class PlayerCurrency
{
    private const int START_AMOUNT = 5000;

    private static readonly ReactiveProperty<int> m_rpAmount = new(START_AMOUNT);
    public static ReadOnlyReactiveProperty<int> Amount => m_rpAmount;

    public static bool TrySpend(int _iCost)
    {
        if (_iCost > m_rpAmount.Value)
            return false;

        m_rpAmount.Value -= _iCost;
        return true;
    }

    public static void Add(int _iValue)
    {
        m_rpAmount.Value += _iValue;
    }
}
