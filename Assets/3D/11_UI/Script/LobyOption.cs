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
    private List<GameObject>[] m_arrPanelGroups;
    private Tween m_refMoveTween;

    private void Start()
    {
        m_arrPanelGroups = new List<GameObject>[m_listOptionButton.Count];
        for (int i = 0; i < m_listOptionButton.Count; ++i)
        {
            m_arrPanelGroups[i] = new List<GameObject>();
            m_listOptionButton[i].CopyClickActivationTargets(m_arrPanelGroups[i]);
            int idx = i;
            m_listOptionButton[i].OnClickEvt.Subscribe(_ => MoveToIdx(idx)).AddTo(this);
        }
    }

    private void OnDestroy() => m_refMoveTween?.Kill();

    private void MoveToIdx(int _idx)
    {
        for (int i = 0; i < m_arrPanelGroups.Length; ++i)
        {
            List<GameObject> listPanels = m_arrPanelGroups[i];
            for (int j = 0; j < listPanels.Count; ++j)
                listPanels[j].SetActive(i == _idx);
        }

        if (m_iCurSelectIdx == _idx)
            return;

        RectTransform refRect = (RectTransform)m_refSelectImage.transform;
        RectTransform refTargetRect = (RectTransform)m_listOptionButton[_idx].transform;
        m_refMoveTween?.Kill();
        m_refMoveTween = refRect.DOAnchorPos(refTargetRect.anchoredPosition, m_fMoveTime).SetEase(Ease.OutQuad);

        m_iCurSelectIdx = _idx;
    }

}
