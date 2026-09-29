using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ClearStageUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TextMeshProUGUI m_refStageText;
    [SerializeField] private TextMeshProUGUI m_refLevelText;
    [SerializeField] private Image m_refExpFill;
    [SerializeField] private Container m_refRewardContainer;
    [SerializeField] private float m_fExpTweenSeconds = 1f;

    private Tween m_refExpTween;
    private bool m_bReturningToLobby;

    public void Initialize()
    {
        m_bReturningToLobby = false;
        m_refExpFill.type = Image.Type.Filled;
        m_refExpFill.fillMethod = Image.FillMethod.Horizontal;
        m_refRewardContainer.Init();
        m_refRewardContainer.GetCategoryData(eDataType.Equip).SetCanDuplication(true);
        m_refRewardContainer.Resize(16, eDataType.Equip);
        gameObject.SetActive(false);
    }

    private void OnDestroy() => m_refExpTween?.Kill();

    public void OnPointerClick(PointerEventData _tEventData)
    {
        if (m_bReturningToLobby)
            return;
        if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)m_refRewardContainer.transform,
            _tEventData.position, _tEventData.pressEventCamera))
            return;
        m_bReturningToLobby = true;
        gameObject.SetActive(false);
        GameSceneManager.m_Instance.LoadFirstScene();
    }

    public void Show(int _iStageNumber, int _iStartExp, int _iEndExp, IReadOnlyList<SOEqipData> _listRewards)
    {
        gameObject.SetActive(true);
        m_refStageText.text = _iStageNumber.ToString();
        m_refExpTween?.Kill();
        float fShownExp = _iStartExp;
        UpdateExp(_iStartExp);
        m_refExpTween = DOTween.To(() => fShownExp, fValue =>
        {
            fShownExp = fValue;
            UpdateExp(Mathf.FloorToInt(fValue));
        }, _iEndExp, m_fExpTweenSeconds).SetEase(Ease.Linear).SetUpdate(true)
            .OnComplete(() => UpdateExp(_iEndExp));

        int iCategoryIdx = m_refRewardContainer.GetCategoryIdx(eDataType.Equip);
        m_refRewardContainer.ClearData(iCategoryIdx);
        m_refRewardContainer.ClearTarget();
        for (int i = 0; i < _listRewards.Count; ++i)
            if (m_refRewardContainer.AddData(_listRewards[i]) == false)
                Debug.LogError("클리어 보상을 컨테이너에 표시하지 못했습니다.", this);
    }

    private void UpdateExp(int _iTotalExp)
    {
        m_refLevelText.text = (_iTotalExp / PlayerLevel.ExpPerLevel + 1).ToString();
        m_refExpFill.fillAmount = (_iTotalExp % PlayerLevel.ExpPerLevel) / (float)PlayerLevel.ExpPerLevel;
    }
}
