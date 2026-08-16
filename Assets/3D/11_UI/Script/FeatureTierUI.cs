using UnityEngine;

/*///////////////////////////////////////////
                FeatureTierUI
목적 : SOFeature 등급(eFeatureTier)을 표시 색상으로 매핑해주는 공용 유틸.
      SlotView/RandomFeatureCard가 동일한 팔레트를 공유하도록 여기 한 곳에서만 관리
 *///////////////////////////////////////////

public static class FeatureTierUI
{
    private static readonly Color[] m_arrTierColor =
    {
        Color.white,                // Common
        new Color(0.3f, 0.9f, 0.3f), // Uncommon
        new Color(0.3f, 0.5f, 1.0f), // Rare
        new Color(1.0f, 0.9f, 0.2f), // Epic
        new Color(0.95f, 0.2f, 0.2f), // Legendary
    };

    public static Color GetColor(eFeatureTier _eTier)
    {
        int iIndex = (int)_eTier;
        if (iIndex < 0 || iIndex >= m_arrTierColor.Length)
            return Color.white;

        return m_arrTierColor[iIndex];
    }
}
