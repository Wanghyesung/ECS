using UnityEngine;

/*///////////////////////////////////////////
              RadarView
목적 : 레이더 원판 위에 몬스터 블립 아이콘 위치를 갱신하는 순수 표시 담당.
       로직 없음 - RadarSystem이 계산한 정규화 좌표를 그대로 그리기만 함
 *///////////////////////////////////////////
public sealed class RadarView : MonoBehaviour
{
    [SerializeField] private RectTransform[] m_arrBlips = null; // 런타임 Instantiate 없이 고정 개수 미리 배치
    [SerializeField] private float m_fRadarPixelRadius = 60f;

    public int MaxBlipCount => m_arrBlips != null ? m_arrBlips.Length : 0;

    // _vNormalizedOffset : 플레이어 기준 상대좌표를 레이더 사거리로 나눈 값 (x,y 각각 -1~1)
    public void SetBlip(int _iIndex, Vector2 _vNormalizedOffset)
    {
        if (m_arrBlips == null || _iIndex < 0 || _iIndex >= m_arrBlips.Length)
            return;

        RectTransform refBlip = m_arrBlips[_iIndex];
        if (refBlip == null)
            return;

        refBlip.anchoredPosition = _vNormalizedOffset * m_fRadarPixelRadius;
        refBlip.gameObject.SetActive(true);
    }

    // 이번 갱신에서 쓰지 않은 나머지 블립은 꺼서 이전 프레임 잔상이 안 남게 함
    public void HideBlipsFrom(int _iStartIndex)
    {
        if (m_arrBlips == null)
            return;

        for (int i = Mathf.Max(_iStartIndex, 0); i < m_arrBlips.Length; ++i)
        {
            if (m_arrBlips[i] != null)
                m_arrBlips[i].gameObject.SetActive(false);
        }
    }
}
