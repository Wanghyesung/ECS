using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/*///////////////////////////////////////////
                ProfileSave
목적 : 시작 시 각 저장 대상의 JSON을 로드하고 변경된 대상의 파일만 기록한다.
      종료 시 백업을 만들고 메인 파일을 읽지 못하면 백업에서 복구한다.
 *///////////////////////////////////////////

public sealed class ProfileSave : MonoBehaviour
{
    private readonly List<ISaveLoadable> m_listSaveables = new List<ISaveLoadable>();
    private readonly HashSet<ISaveLoadable> m_hashBlockedFiles = new HashSet<ISaveLoadable>();
    private readonly HashSet<ISaveLoadable> m_hashDirty = new HashSet<ISaveLoadable>();

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
        for (int i = 0; i < m_listSaveables.Count; ++i)
            m_hashDirty.Add(m_listSaveables[i]);

        DeleteOldProfileFiles();
        LoadAll();
        Save();
    }

    private void OnApplicationQuit()
    {
        Save();
        BackupAll();
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
        for (int i = 0; i < m_listSaveables.Count; ++i)
        {
            ISaveLoadable refSaveable = m_listSaveables[i];
            string strPath = GetSavePath(refSaveable);
            if (TryRead(refSaveable, strPath))
            {
                m_hashDirty.Remove(refSaveable);
                continue;
            }

            string strBackupPath = strPath + ".bak";
            if (TryRead(refSaveable, strBackupPath))
            {
                Save(refSaveable);
                continue;
            }

            if (File.Exists(strPath) == false && File.Exists(strBackupPath) == false)
                continue;

            m_hashBlockedFiles.Add(refSaveable);
            Debug.LogError($"{refSaveable.FileName}을 복원할 수 없어 덮어쓰기를 막았습니다.");
        }
    }

    private bool TryRead(ISaveLoadable _refSaveable, string _strPath)
    {
        if (File.Exists(_strPath) == false)
            return false;

        try
        {
            if (_refSaveable.Load(File.ReadAllText(_strPath)))
                return true;
            Debug.LogWarning($"세이브 형식이 올바르지 않습니다: {_strPath}");
        }
        catch (Exception refError)
        {
            Debug.LogWarning($"세이브 로드 실패 ({_strPath}): {refError.Message}");
        }
        return false;
    }

    public void Save()
    {
        for (int i = 0; i < m_listSaveables.Count; ++i)
        {
            ISaveLoadable refSaveable = m_listSaveables[i];
            if (m_hashDirty.Contains(refSaveable))
                Save(refSaveable);
        }
    }

    public void MarkDirty(ISaveLoadable _refSaveable)
    {
        if (m_listSaveables.Contains(_refSaveable))
            m_hashDirty.Add(_refSaveable);
    }

    public void Save(ISaveLoadable _refSaveable)
    {
        if (m_listSaveables.Contains(_refSaveable) == false || m_hashBlockedFiles.Contains(_refSaveable))
            return;

        m_hashDirty.Add(_refSaveable);
        string strPath = GetSavePath(_refSaveable);
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(strPath, _refSaveable.Save());
            m_hashDirty.Remove(_refSaveable);
        }
        catch (Exception refError)
        {
            Debug.LogError($"{_refSaveable.FileName} 저장 실패: {refError.Message}");
        }
    }

    private void BackupAll()
    {
        for (int i = 0; i < m_listSaveables.Count; ++i)
        {
            ISaveLoadable refSaveable = m_listSaveables[i];
            if (m_hashBlockedFiles.Contains(refSaveable) || m_hashDirty.Contains(refSaveable))
                continue;

            string strPath = GetSavePath(refSaveable);
            if (File.Exists(strPath) == false)
                continue;

            try
            {
                File.Copy(strPath, strPath + ".bak", true);
            }
            catch (Exception refError)
            {
                Debug.LogError($"{refSaveable.FileName} 백업 실패: {refError.Message}");
            }
        }
    }

    private void DeleteOldProfileFiles()
    {
        string strPath = Path.Combine(Application.persistentDataPath, "profile.json");
        string[] arrPaths = { strPath, strPath + ".tmp", strPath + ".bak" };
        for (int i = 0; i < arrPaths.Length; ++i)
        {
            try
            {
                File.Delete(arrPaths[i]);
            }
            catch (Exception refError)
            {
                Debug.LogWarning($"이전 저장 파일 삭제 실패 ({arrPaths[i]}): {refError.Message}");
            }
        }
    }
}
