using System;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                PlayerPreLoadData
목적 : 영구 성장과 장비 보너스를 분리해 보관하고 런 시작 시 Player에게 적용한다.
      영구 성장만 저장하며 장비 보너스는 장착 ID를 통해 다시 계산한다.
 *///////////////////////////////////////////

public sealed class PlayerPreLoadData : ISaveLoadable
{
    [Serializable]
    private sealed class ProgressData
    {
        [SerializeField] private int m_iVersion = 1;
        [SerializeField] private List<tStatValue> m_listPermanentStats = new List<tStatValue>();

        public int Version => m_iVersion;
        public List<tStatValue> PermanentStats { get => m_listPermanentStats; set => m_listPermanentStats = value; }
    }

    private readonly ProfileSave m_refSaveManager;
    private readonly List<tStatValue> m_listPermanentStat = new List<tStatValue>();
    private readonly List<tStatValue> m_listEquipmentStat = new List<tStatValue>();
    private readonly ProgressData m_refData = new ProgressData();

    public string FileName => "stats.json";

    public PlayerPreLoadData(ProfileSave _refSaveManager)
    {
        m_refSaveManager = _refSaveManager;
    }

    private void AddStatRange(IReadOnlyList<tStatValue> _listValue)
    {
        for (int i = 0; i < _listValue.Count; ++i)
            m_listEquipmentStat.Add(_listValue[i]);
    }

    public void SetEquipmentStats(IReadOnlyList<tStatValue> _listValue)
    {
        m_listEquipmentStat.Clear();
        AddStatRange(_listValue);
    }

    public eSaveLoadResult Load(string _strJson)
    {
        ProgressData refData;
        try
        {
            refData = JsonUtility.FromJson<ProgressData>(_strJson);
        }
        catch (Exception)
        {
            return eSaveLoadResult.Invalid;
        }

        if (refData == null)
            return eSaveLoadResult.Invalid;
        if (refData.Version > 1)
            return eSaveLoadResult.UnsupportedVersion;
        if (refData.Version != 1 || refData.PermanentStats == null)
            return eSaveLoadResult.Invalid;

        m_listPermanentStat.Clear();
        m_listPermanentStat.AddRange(refData.PermanentStats);
        m_listEquipmentStat.Clear();
        return eSaveLoadResult.Loaded;
    }

    public string Save()
    {
        m_refData.PermanentStats = m_listPermanentStat;
        return JsonUtility.ToJson(m_refData);
    }

    public void LoadLegacy(IReadOnlyList<tStatValue> _listPermanentStats)
    {
        m_listPermanentStat.Clear();
        AddPermanentStats(_listPermanentStats);
        m_listEquipmentStat.Clear();
    }

    private void AddPermanentStats(IReadOnlyList<tStatValue> _listPermanentStats)
    {
        for (int i = 0; i < _listPermanentStats.Count; ++i)
            m_listPermanentStat.Add(_listPermanentStats[i]);
    }

    // 로비 스탯 강화창처럼 한 번에 한 스탯만 추가할 때, 배열로 감싸지 않고 바로 쓰기 위한 편의 메서드
    public void AddStat(eStatType _eType, float _fValue)
    {
        m_listPermanentStat.Add(new tStatValue { Type = _eType, Value = _fValue });
        m_refSaveManager.Save(this);
    }

    public void ApplyTo(Player _refPlayer)
    {
        for (int i = 0; i < m_listPermanentStat.Count; ++i)
            ApplyStat(_refPlayer, m_listPermanentStat[i]);
        for (int i = 0; i < m_listEquipmentStat.Count; ++i)
            ApplyStat(_refPlayer, m_listEquipmentStat[i]);
    }

    // 로비 스탯창에서 Player 없이 영구 성장과 장비 보너스의 합계를 조회할 때 사용
    public float GetPendingTotal(eStatType _eType)
    {
        float fTotal = GetPermanentTotal(_eType);
        for (int i = 0; i < m_listEquipmentStat.Count; ++i)
        {
            if (m_listEquipmentStat[i].Type == _eType)
                fTotal += m_listEquipmentStat[i].Value;
        }
        return fTotal;
    }

    public float GetPermanentTotal(eStatType _eType)
    {
        float fTotal = 0f;
        for (int i = 0; i < m_listPermanentStat.Count; ++i)
        {
            if (m_listPermanentStat[i].Type == _eType)
                fTotal += m_listPermanentStat[i].Value;
        }
        return fTotal;
    }

    private void ApplyStat(Player _refPlayer, tStatValue _tModifier)
    {
        switch (_tModifier.Type)
        {
            case eStatType.HP:
                _refPlayer.AddMaxHP((long)_tModifier.Value);
                break;
            case eStatType.Attack:
                _refPlayer.AddAttack((int)_tModifier.Value);
                break;
            case eStatType.Defense:
                _refPlayer.AddDefense(_tModifier.Value);
                break;
            case eStatType.Speed:
                _refPlayer.AddSpeed(_tModifier.Value);
                break;
            case eStatType.BulletSpeed:
                _refPlayer.UpBulletSpeed(_tModifier.Value);
                break;
        }
    }
}
