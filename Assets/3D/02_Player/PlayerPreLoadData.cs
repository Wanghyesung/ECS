using System.Collections.Generic;

/*///////////////////////////////////////////
                PlayerPreLoadData
기능 : 로비 씬(EquipController)에서 선택된 장비 스탯을, Player가 존재하지 않는
      씬 전환 구간을 넘어 메인 씬의 Player에게 전달하기 위한 정적 버퍼.
      static 클래스이므로 GameObject 파괴와 무관하게 프로세스 생존 기간 동안 값을 유지한다.
 *///////////////////////////////////////////

public static class PlayerPreLoadData
{
    private static List<tStatValue> m_listPendingStat = new List<tStatValue>();

    public static void AddStatRange(IReadOnlyList<tStatValue> _listValue)
    {
        for (int i = 0; i < _listValue.Count; ++i)
            m_listPendingStat.Add(_listValue[i]);
    }

    // 로비 스탯 강화창처럼 한 번에 한 스탯만 추가할 때, 배열로 감싸지 않고 바로 쓰기 위한 편의 메서드
    public static void AddStat(eStatType _eType, float _fValue)
    {
        m_listPendingStat.Add(new tStatValue { Type = _eType, Value = _fValue });
    }

    public static void ApplyTo(Player _refPlayer)
    {
        for (int i = 0; i < m_listPendingStat.Count; ++i)
            ApplyStat(_refPlayer, m_listPendingStat[i]);
    }

    // 로비 스탯창처럼 Player가 존재하지 않는 상태에서 누적된 장비 보너스 합계만 조회할 때 사용
    public static float GetPendingTotal(eStatType _eType)
    {
        float fTotal = 0f;
        for (int i = 0; i < m_listPendingStat.Count; ++i)
        {
            if (m_listPendingStat[i].Type == _eType)
                fTotal += m_listPendingStat[i].Value;
        }
        return fTotal;
    }

    private static void ApplyStat(Player _refPlayer, tStatValue _tModifier)
    {
        switch (_tModifier.Type)
        {
            case eStatType.HP:
                _refPlayer.AddMaxHP((long)_tModifier.Value);
                break;
            case eStatType.Attack:
                _refPlayer.AddAttack((int)_tModifier.Value);
                break;
            case eStatType.Defense:
                _refPlayer.AddDefense(_tModifier.Value);
                break;
            case eStatType.Speed:
                _refPlayer.AddSpeed(_tModifier.Value);
                break;
            case eStatType.BulletSpeed:
                _refPlayer.UpBulletSpeed(_tModifier.Value);
                break;
        }
    }
}
