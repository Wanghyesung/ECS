using DG.Tweening;
using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.UI;

public class LobyOption : MonoBehaviour
{
    [SerializeField] private List<BaseButtonUI> m_listOptionButton;
    [SerializeField] private Image m_refSelectImage;
    [SerializeField] private float m_fMoveTime;

    private int m_iCurSelectIdx = -1;

    private void Start()
    {
        for (int i = 0; i < m_listOptionButton.Count; ++i)
        {
            int idx = i;
            m_listOptionButton[i].OnClickEvt.Subscribe(_ => MoveToIdx(idx)).AddTo(this);
        }
    }

    private void MoveToIdx(int _idx)
    {
        if (m_iCurSelectIdx == _idx)
            return;

        RectTransform refRect = (RectTransform)m_refSelectImage.transform;
        RectTransform refTargetRect = (RectTransform)m_listOptionButton[_idx].transform;
        refRect.DOAnchorPos(refTargetRect.anchoredPosition, m_fMoveTime).SetEase(Ease.OutQuad);

        m_iCurSelectIdx = _idx;
    }

}
