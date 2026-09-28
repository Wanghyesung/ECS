using System;
using R3;
using UnityEngine;

/*///////////////////////////////////////////
                PlayerCurrency
목적 : 플레이어 골드를 보관하고 변경 사항을 UI에 알리며 저장을 요청한다.
      ProfileSave의 로드·저장 순회에서 골드 값을 담당한다.
 *///////////////////////////////////////////

public sealed class PlayerCurrency : ISaveLoadable
{
    [Serializable]
    private sealed class CurrencyData
    {
        [SerializeField] private int m_iVersion = 1;
        [SerializeField] private int m_iGold = START_AMOUNT;

        public int Version => m_iVersion;
        public int Gold { get => m_iGold; set => m_iGold = value; }
    }

    private const int START_AMOUNT = 5000;
    private readonly ProfileSave m_refSaveManager;
    private readonly ReactiveProperty<int> m_rpAmount = new(START_AMOUNT);
    private CurrencyData m_refData = new CurrencyData();

    public string FileName => "currency.json";
    public ReadOnlyReactiveProperty<int> Amount => m_rpAmount;

    public PlayerCurrency(ProfileSave _refSaveManager)
    {
        m_refSaveManager = _refSaveManager;
    }

    public bool TrySpend(int _iCost, bool _bSaveImmediately = true)
    {
        if (_iCost < 0 || _iCost > m_rpAmount.Value)
            return false;

        m_rpAmount.Value -= _iCost;
        if (_bSaveImmediately)
            m_refSaveManager.Save(this);
        return true;
    }

    public void Add(int _iValue)
    {
        if (_iValue <= 0)
            return;

        m_rpAmount.Value = _iValue > int.MaxValue - m_rpAmount.Value
            ? int.MaxValue : m_rpAmount.Value + _iValue;
        m_refSaveManager.Save(this);
    }

    public eSaveLoadResult Load(string _strJson)
    {
        try
        {
            CurrencyData refData = JsonUtility.FromJson<CurrencyData>(_strJson);
            if (refData == null || refData.Version < 1 || refData.Gold < 0)
                return eSaveLoadResult.Invalid;
            if (refData.Version > 1)
                return eSaveLoadResult.UnsupportedVersion;

            m_refData = refData;
            m_rpAmount.Value = refData.Gold;
            return eSaveLoadResult.Loaded;
        }
        catch (Exception)
        {
            return eSaveLoadResult.Invalid;
        }
    }

    public string Save()
    {
        m_refData.Gold = m_rpAmount.Value;
        return JsonUtility.ToJson(m_refData);
    }

    public void LoadLegacy(int _iGold)
    {
        m_rpAmount.Value = _iGold;
    }

    public void Dispose()
    {
        m_rpAmount.Dispose();
    }
}
