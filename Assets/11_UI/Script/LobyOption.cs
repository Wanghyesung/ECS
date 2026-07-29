using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class LobyOption : MonoBehaviour
{
    [SerializeField] private List<BaseButtonUI> m_listOptionButton;
    [SerializeField] private Image m_refSelectImage;
    [SerializeField] private float m_fMoveTime;

    private int m_iCurSelectIdx;
    private List<Action> m_listClickAction = new();

    private void Start()
    {
        for (int i = 0; i < m_listOptionButton.Count; ++i)
        {
            int idx = i;
            Action clickAction = () => MoveToIdx(idx);
            m_listClickAction.Add(clickAction);
            m_listOptionButton[i].OnClickEvt += clickAction;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < m_listOptionButton.Count; ++i)
        {
            m_listOptionButton[i].OnClickEvt -= m_listClickAction[i];
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
