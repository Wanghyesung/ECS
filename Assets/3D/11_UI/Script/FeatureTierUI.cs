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
        new Color(0.75f, 0.75f, 0.75f), // Common
        new Color(0.35f, 0.75f, 0.35f), // Uncommon
        new Color(0.30f, 0.55f, 0.95f), // Rare
        new Color(0.65f, 0.35f, 0.90f), // Epic
        new Color(0.95f, 0.70f, 0.20f), // Legendary
    };

    public static Color GetColor(eFeatureTier _eTier)
    {
        int iIndex = (int)_eTier;
        if (iIndex < 0 || iIndex >= m_arrTierColor.Length)
            return Color.white;

        return m_arrTierColor[iIndex];
    }
}
