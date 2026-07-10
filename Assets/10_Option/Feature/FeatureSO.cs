using System;
using UnityEngine;

[Serializable]
public enum eFeatureID
{
    None,
    AttackSpeedUp,
    UnlockMissile,
    End,
}

[Serializable]
public enum eAcquireType
{
    Repeatable,
    OneTime,
}

/*///////////////////////////////////////////
                FeatureSO
기능 : 레벨업 등 특정 상황에 랜덤으로 제시되는 강화/기능의 정적 데이터 + 적용 로직을 담는 베이스 SO
      런타임 획득 상태(레벨, 획득 여부)는 SO가 아닌 FeatureManager가 보유한다 (SO 데이터 오염 방지)
 *///////////////////////////////////////////

public abstract class FeatureSO : ScriptableObject
{
    [SerializeField] private eFeatureID m_eID = eFeatureID.None;
    public eFeatureID ID => m_eID;


    [TextArea]
    [SerializeField] private string m_strDescription;

    [SerializeField] private Sprite m_sprIcon;
    public Sprite Icon => m_sprIcon;

    [SerializeField] private eAcquireType m_eAcquireType = eAcquireType.Repeatable;
    public eAcquireType AcquireType => m_eAcquireType;

    [Tooltip("후보 추첨 시 가중치. 전부 1이면 균등 랜덤")]
    [SerializeField] private int m_iWeight = 1;
    public int Weight => m_iWeight;

    // _iNewLevel : 이 기능을 선택한 시점의 누적 획득 횟수 (FeatureManager가 갱신 후 전달)
    public abstract void Apply(Player _refPlayer, int _iNewLevel);
}
