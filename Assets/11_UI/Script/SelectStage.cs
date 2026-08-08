using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/*///////////////////////////////////////////
                SelectStage
기능 : 스테이지 선택 화면. RectMask2D로 잘린 콘텐츠 안에 스테이지 이미지를
      가로로 나열해두고, 이전/다음 버튼을 누르면 DOTween으로 한 장씩(오프셋 500)
      캐러셀처럼 넘겨볼 수 있다.
      GameSceneManager를 통해 게임 씬으로 넘어간다.
 *///////////////////////////////////////////
public class SelectStage : MonoBehaviour
{
    [SerializeField] private RectTransform m_refContentView; //RectMask2D 안에서 실제로 움직이는 콘텐츠
    [SerializeField] private List<BaseButtonUI> m_listStageImage;
    [SerializeField] private BaseButtonUI m_refPrevBtn;
    [SerializeField] private BaseButtonUI m_refNextBtn;
    [SerializeField] private BaseButtonUI m_refStartBtn;

    [SerializeField] private float m_fPageOffset = 500f;
    [SerializeField] private float m_fTweenDuration = 0.3f;

    [SerializeField] private TextMeshProUGUI m_refStageNameText;
    [SerializeField] private TextMeshProUGUI m_refStageLevelText;
   
    private int m_iViewIdx = 0;   //현재 캐러셀에서 보고 있는 위치
    private bool m_bSwap = false;

    private void Awake()
    {
      
        if (m_refPrevBtn != null)
            m_refPrevBtn.OnClickEvt += MovePrev;

        if (m_refNextBtn != null)
            m_refNextBtn.OnClickEvt += MoveNext;

        if (m_refStartBtn != null)
            m_refStartBtn.OnClickEvt += StartStage;

    }

    private void OnDestroy()
    {

        if (m_refPrevBtn != null)
            m_refPrevBtn.OnClickEvt -= MovePrev;

        if (m_refNextBtn != null)
            m_refNextBtn.OnClickEvt -= MoveNext;

        if(m_refStartBtn != null)
            m_refStartBtn.OnClickEvt -= StartStage;
    }

    /*/////////////////////////////////////
                  버튼 네비게이션
    *////////////////////////////////////

    private void MoveNext()
    {
        if (m_bSwap || m_iViewIdx >= m_listStageImage.Count - 1)
            return;

        ++m_iViewIdx;
        MoveToView();
    }

    private void MovePrev()
    {
        if (m_bSwap || m_iViewIdx <= 0)
            return;

        --m_iViewIdx;
        MoveToView();
    }

    private void MoveToView()
    {
        m_bSwap = true;

        float fTargetX = -m_iViewIdx * m_fPageOffset;
        m_refContentView.DOAnchorPosX(fTargetX, m_fTweenDuration)
            .SetEase(Ease.OutCubic)
            .OnComplete(() => m_bSwap = false);

        m_refStageNameText.text = m_listStageImage[m_iViewIdx].name;
        m_refStageLevelText.text = (m_iViewIdx+1).ToString();
    }

 
    private void StartStage()
    {
        GameSceneManager.m_Instance.LoadStage(m_iViewIdx);
    }
}
