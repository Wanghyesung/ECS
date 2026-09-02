using UnityEngine;

/*///////////////////////////////////////////
              OffscreenIndicatorView
목적 : 탐지 범위 안이지만 카메라 시야 밖인 몬스터 방향으로 화면 가장자리에 화살표 표시.
       로직 없음 - RadarSystem이 계산한 좌표/각도를 그대로 그리기만 함
 *///////////////////////////////////////////
public sealed class OffscreenIndicatorView : MonoBehaviour
{
    [SerializeField] private RectTransform m_refRoot = null; // 화면 전체를 덮는 기준 RectTransform (중심 기준 좌표계)
    [SerializeField] private RectTransform[] m_arrArrows = null; // 런타임 Instantiate 없이 고정 개수 미리 배치
    [SerializeField] private float m_fEdgePadding = 60f;

    public RectTransform Root => m_refRoot;
    public int MaxArrowCount => m_arrArrows != null ? m_arrArrows.Length : 0;
    public float EdgePadding => m_fEdgePadding;

    public void SetArrow(int _iIndex, Vector2 _vClampedPos, float _fAngle)
    {
        if (m_arrArrows == null || _iIndex < 0 || _iIndex >= m_arrArrows.Length)
            return;

        RectTransform refArrow = m_arrArrows[_iIndex];
        if (refArrow == null)
            return;

        refArrow.anchoredPosition = _vClampedPos;
        refArrow.localRotation = Quaternion.Euler(0f, 0f, _fAngle);
        refArrow.gameObject.SetActive(true);
    }

    // 이번 갱신에서 쓰지 않은 나머지 화살표는 꺼서 이전 프레임 잔상이 안 남게 함
    public void HideArrowsFrom(int _iStartIndex)
    {
        if (m_arrArrows == null)
            return;

        for (int i = Mathf.Max(_iStartIndex, 0); i < m_arrArrows.Length; ++i)
        {
            if (m_arrArrows[i] != null)
                m_arrArrows[i].gameObject.SetActive(false);
        }
    }
}
