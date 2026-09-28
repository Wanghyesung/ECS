using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/*///////////////////////////////////////////
                ProfileSave
목적 : 시작 시 각 저장 대상의 JSON을 로드하고 변경된 대상의 파일만 기록한다.
      이전 profile.json 형식을 첫 로드 시 옮기며 임시·백업 파일로 복구한다.
 *///////////////////////////////////////////

public sealed class ProfileSave : MonoBehaviour
{
    private const string LEGACY_FILE_NAME = "profile.json";
    private const int LEGACY_SAVE_VERSION = 1;

    private readonly List<ISaveLoadable> m_listSaveables = new List<ISaveLoadable>();
    private readonly HashSet<ISaveLoadable> m_hashHealthyFiles = new HashSet<ISaveLoadable>();
    private readonly HashSet<ISaveLoadable> m_hashBlockedFiles = new HashSet<ISaveLoadable>();

    public static ProfileSave m_Instance { get; private set; }
    public PlayerCurrency Currency { get; private set; }
    public PlayerPreLoadData PreLoad { get; private set; }
    public PlayerInventory Inventory { get; private set; }

    private string GetSavePath(ISaveLoadable _refSaveable) =>
        Path.Combine(Application.persistentDataPath, _refSaveable.FileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (m_Instance != null)
            return;

        GameObject refRoot = new GameObject(nameof(ProfileSave));
        refRoot.AddComponent<ProfileSave>();
    }

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(gameObject);

        SOEquipCatalog refCatalog = Resources.Load<SOEquipCatalog>("EquipCatalog");
        if (refCatalog == null)
            Debug.LogError("EquipCatalog 리소스가 없어 장비 저장을 복원할 수 없습니다.");

        Currency = new PlayerCurrency(this);
        PreLoad = new PlayerPreLoadData(this);
        Inventory = new PlayerInventory(this, PreLoad, refCatalog);
        // Inventory.Load가 장착 보너스를 PreLoad에 반영하므로 PreLoad를 먼저 로드한다.
        m_listSaveables.Add(Currency);
        m_listSaveables.Add(PreLoad);
        m_listSaveables.Add(Inventory);

        LoadAll();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    private void OnDestroy()
    {
        if (m_Instance != this)
            return;

        Currency.Dispose();
        m_Instance = null;
    }

    private void LoadAll()
    {
        PlayerProfile refLegacyProfile = null;
        bool bLegacyChecked = false;
        for (int i = 0; i < m_listSaveables.Count; ++i)
        {
            ISaveLoadable refSaveable = m_listSaveables[i];
            if (TryLoad(refSaveable, out bool bFileExists) || m_hashBlockedFiles.Contains(refSaveable))
                continue;

            if (bLegacyChecked == false)
            {
                string strLegacyPath = Path.Combine(Application.persistentDataPath, LEGACY_FILE_NAME);
                refLegacyProfile = TryReadLegacy(strLegacyPath)
                    ?? TryReadLegacy(strLegacyPath + ".tmp")
                    ?? TryReadLegacy(strLegacyPath + ".bak");
                bLegacyChecked = true;
            }

            if (refLegacyProfile != null)
            {
                LoadLegacy(refSaveable, refLegacyProfile);
                Save(refSaveable);
            }
            else if (bFileExists)
            {
                m_hashBlockedFiles.Add(refSaveable);
                Debug.LogError($"{refSaveable.FileName}을 복원할 수 없어 덮어쓰기를 막았습니다.");
            }
        }
    }

    private bool TryLoad(ISaveLoadable _refSaveable, out bool _bFileExists)
    {
        string strPath = GetSavePath(_refSaveable);
        string[] arrPaths = { strPath, strPath + ".tmp", strPath + ".bak" };
        _bFileExists = false;
        for (int i = 0; i < arrPaths.Length; ++i)
        {
            string strCandidate = arrPaths[i];
            if (File.Exists(strCandidate) == false)
                continue;

            _bFileExists = true;
            try
            {
                eSaveLoadResult eResult = _refSaveable.Load(File.ReadAllText(strCandidate));
                if (eResult == eSaveLoadResult.UnsupportedVersion)
                {
                    m_hashBlockedFiles.Add(_refSaveable);
                    Debug.LogError($"지원하지 않는 저장 버전: {strCandidate}. 파일을 덮어쓰지 않습니다.");
                    return false;
                }
                if (eResult != eSaveLoadResult.Loaded)
                    continue;

                if (i > 0)
                {
                    try
                    {
                        File.Copy(strCandidate, strPath, true);
                    }
                    catch (Exception refError)
                    {
                        Debug.LogWarning($"세이브 복구 실패 ({strCandidate}): {refError.Message}");
                        return true;
                    }
                }
                m_hashHealthyFiles.Add(_refSaveable);
                return true;
            }
            catch (Exception refError)
            {
                Debug.LogWarning($"세이브 로드·복구 실패 ({strCandidate}): {refError.Message}");
            }
        }
        return false;
    }

    public void Save()
    {
        for (int i = 0; i < m_listSaveables.Count; ++i)
            Save(m_listSaveables[i]);
    }

    public void Save(ISaveLoadable _refSaveable)
    {
        if (m_listSaveables.Contains(_refSaveable) == false || m_hashBlockedFiles.Contains(_refSaveable))
            return;

        string strPath = GetSavePath(_refSaveable);
        string strTempPath = strPath + ".tmp";
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(strTempPath, _refSaveable.Save());
            if (m_hashHealthyFiles.Contains(_refSaveable) && File.Exists(strPath))
                File.Copy(strPath, strPath + ".bak", true);
            File.Copy(strTempPath, strPath, true);
            m_hashHealthyFiles.Add(_refSaveable);
            File.Delete(strTempPath);
        }
        catch (Exception refError)
        {
            Debug.LogError($"{_refSaveable.FileName} 저장 실패: {refError.Message}");
        }
    }

    private void LoadLegacy(ISaveLoadable _refSaveable, PlayerProfile _refProfile)
    {
        if (_refSaveable == Currency)
            Currency.LoadLegacy(_refProfile.Gold);
        else if (_refSaveable == PreLoad)
            PreLoad.LoadLegacy(_refProfile.PermanentStats);
        else if (_refSaveable == Inventory)
            Inventory.LoadLegacy(_refProfile.InventoryItemIds, _refProfile.EquippedItemIds);
    }

    private PlayerProfile TryReadLegacy(string _strPath)
    {
        if (File.Exists(_strPath) == false)
            return null;

        try
        {
            PlayerProfile refProfile = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(_strPath));
            if (refProfile == null || refProfile.Version != LEGACY_SAVE_VERSION || refProfile.Gold < 0)
                return null;
            if (refProfile.PermanentStats == null || refProfile.InventoryItemIds == null)
                return null;
            if (refProfile.EquippedItemIds == null || refProfile.EquippedItemIds.Length != (int)eEquipType.Shoes + 1)
                return null;
            return refProfile;
        }
        catch (Exception refError)
        {
            Debug.LogWarning($"프로필 로드 실패 ({_strPath}): {refError.Message}");
            return null;
        }
    }
}
