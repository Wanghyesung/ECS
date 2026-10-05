using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LobbyLevelView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI m_refLevelText;
    [SerializeField] private Image m_refExpFill;

    private void Start()
    {
        PlayerLevel refLevel = ProfileSave.m_Instance.Level;
        m_refLevelText.text = refLevel.Level.ToString();
        m_refExpFill.fillAmount = refLevel.Exp / (float)PlayerLevel.ExpPerLevel;
    }
}
