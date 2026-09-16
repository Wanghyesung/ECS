using UnityEngine;

/*///////////////////////////////////////////
                FeatureTierUI
목적 : SOFeature 등급(eFeatureTier)을 표시 색상으로 매핑해주는 공용 유틸.
      SlotView/RandomFeatureCard가 동일한 팔레트를 공유하도록 여기 한 곳에서만 관리
 *///////////////////////////////////////////

public static class FeatureTierUI
{
    // 오비탈_기획서_v2 §05 등급표의 ● 색상 그대로 (hex → 0~1)
    private static readonly Color[] m_arrTierColor =
    {
        new Color(0.604f, 0.627f, 0.651f), // Common    #9AA0A6
        new Color(0.239f, 0.639f, 0.365f), // Uncommon  #3DA35D
        new Color(0.231f, 0.435f, 0.839f), // Rare      #3B6FD6
        new Color(0.541f, 0.310f, 0.839f), // Epic      #8A4FD6
        new Color(0.839f, 0.651f, 0.231f), // Legendary #D6A63B
    };

    public static Color GetColor(eFeatureTier _eTier)
    {
        int iIndex = (int)_eTier;
        if (iIndex < 0 || iIndex >= m_arrTierColor.Length)
            return Color.white;

        return m_arrTierColor[iIndex];
    }
}
