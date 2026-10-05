using System;
using UnityEngine;

// 로비 레벨은 던전 한 판의 BattleManager 레벨과 별도로 저장한다.
public sealed class PlayerLevel : ISaveLoadable
{
    [Serializable]
    private sealed class tSaveData
    {
        [SerializeField] private int m_iTotalExp;
        public int TotalExp { get => m_iTotalExp; set => m_iTotalExp = value; }
    }

    public const int ExpPerLevel = 100;
    private readonly ProfileSave m_refSaveManager;
    private readonly tSaveData m_refData = new tSaveData();

    public string FileName => "level.json";
    public int TotalExp => m_refData.TotalExp;
    public int Level => TotalExp / ExpPerLevel + 1;
    public int Exp => TotalExp % ExpPerLevel;

    public PlayerLevel(ProfileSave _refSaveManager) => m_refSaveManager = _refSaveManager;

    public bool Load(string _strJson)
    {
        if (string.IsNullOrEmpty(_strJson) || _strJson.Contains("\"m_iTotalExp\"") == false)
            return false;
        try
        {
            JsonUtility.FromJsonOverwrite(_strJson, m_refData);
            m_refData.TotalExp = Mathf.Max(0, m_refData.TotalExp);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public string Save() => JsonUtility.ToJson(m_refData);

    public void AddExp(int _iAmount)
    {
        if (_iAmount <= 0)
            return;
        m_refData.TotalExp = (int)Math.Min(int.MaxValue, (long)TotalExp + _iAmount);
        m_refSaveManager.Save(this);
    }
}
