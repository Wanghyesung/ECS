using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/*///////////////////////////////////////////
                SelectStage
기능 : 스테이지 선택 화면. RectMask2D로 잘린 콘텐츠 안에 스테이지 이미지를
      가로로 나열해두고, 이전/다음 버튼을 누르면 DOTween으로 한 장씩(오프셋 500)
      캐러셀처럼 넘겨볼 수 있다. 보이는 이미지를 직접 클릭하면 그 스테이지를 골라
      GameSceneManager를 통해 게임 씬으로 넘어간다.
 *///////////////////////////////////////////
public class SelectStage : MonoBehaviour
{
    [SerializeField] private RectTransform m_refContentView; //RectMask2D 안에서 실제로 움직이는 콘텐츠
    [SerializeField] private List<BaseButtonUI> m_listStageImage = new List<BaseButtonUI>();

    [SerializeField] private BaseButtonUI m_refPrevBtn;
    [SerializeField] private BaseButtonUI m_refNextBtn;

    [SerializeField] private float m_fPageOffset = 500f;
    [SerializeField] private float m_fTweenDuration = 0.3f;

    private Action[] m_arrImageClickHandler; //Awake에서 등록한 델리게이트를 OnDestroy에서 그대로 해제하기 위한 버퍼
    private int m_iViewIdx = 0;   //현재 캐러셀에서 보고 있는 위치
    private bool m_bSwap = false;

    private void Awake()
    {
        m_arrImageClickHandler = new Action[m_listStageImage.Count];

        for (int i = 0; i < m_listStageImage.Count; ++i)
        {
            int iStageIdx = i; //클로저 캡처용 로컬 복사 (i를 그대로 캡처하면 전부 마지막 값을 참조하게 됨)
            m_arrImageClickHandler[i] = () => OnClickStageImage(iStageIdx);
            m_listStageImage[i].OnClickEvt += m_arrImageClickHandler[i];
        }

        if (m_refPrevBtn != null)
            m_refPrevBtn.OnClickEvt += MovePrev;

        if (m_refNextBtn != null)
            m_refNextBtn.OnClickEvt += MoveNext;
    }

    private void OnDestroy()
    {
        if (m_arrImageClickHandler != null)
        {
            for (int i = 0; i < m_listStageImage.Count; ++i)
                m_listStageImage[i].OnClickEvt -= m_arrImageClickHandler[i];
        }

        if (m_refPrevBtn != null)
            m_refPrevBtn.OnClickEvt -= MovePrev;

        if (m_refNextBtn != null)
            m_refNextBtn.OnClickEvt -= MoveNext;
    }

    /*/////////////////////////////////////
                  버튼 네비게이션
    *////////////////////////////////////

    private void MoveNext()
    {
        if (m_bSwap || m_iViewIdx >= m_listStageImage.Count - 1)
            return;

        ++m_iViewIdx;
        TweenToView();
    }

    private void MovePrev()
    {
        if (m_bSwap || m_iViewIdx <= 0)
            return;

        --m_iViewIdx;
        
        TweenToView();
    }

    private void TweenToView()
    {
        m_bSwap = true;

        float fTargetX = -m_iViewIdx * m_fPageOffset;
        m_refContentView.DOAnchorPosX(fTargetX, m_fTweenDuration)
            .SetEase(Ease.OutCubic)
            .OnComplete(() => m_bSwap = false);
    }

  
    private void OnClickStageImage(int _iStageIdx)
    {
        if (GameSceneManager.m_Instance == null)
        {
            Debug.Log("GameSceneManager 미존재 : SelectStage");
            return;
        }

        GameSceneManager.m_Instance.LoadStage(_iStageIdx);
    }
}
