using UnityEngine;

/*///////////////////////////////////////////
                SOData
기능 : Container/SlotView 등 공용 슬롯 UI가 다루는 모든 SO의 최상위 부모.
      표시용 공통 데이터(설명, 아이콘)만 가지고, 실제 동작/기능은 자식 클래스(SOFeature, SOJokerCard 등)가 담당
 *///////////////////////////////////////////

public abstract class SOData : ScriptableObject
{
    [TextArea]
    [SerializeField] private string m_strDescription;
    public string Description => m_strDescription;

    [SerializeField] private Sprite m_sprIcon;
    public Sprite Icon => m_sprIcon;
}
