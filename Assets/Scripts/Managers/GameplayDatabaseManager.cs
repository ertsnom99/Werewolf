using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameplayDatabaseManager : MonoSingleton<GameplayDatabaseManager>
{
    [SerializeField]
    private string[] _foldersToload;

    private Dictionary<int, GameplayData> _IDtoGameplayData = new Dictionary<int, GameplayData>();

    [field: SerializeField]
    [field: ReadOnly]
    public bool IsReady { get; private set; }

#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField]
    private bool _logDatabase = false;
#endif

    protected override void Awake()
    {
        base.Awake();

        _IDtoGameplayData.Clear();

        foreach (string folder in _foldersToload)
        {
            LoadGameplayDatas(folder);
        }

        IsReady = true;
#if UNITY_EDITOR
        if (!_logDatabase)
        {
            return;
        }

        LogDatabase();
#endif
    }

#if UNITY_EDITOR
    private void LogDatabase()
    {
        Debug.Log("------------------GameplayData Database------------------");

        foreach (KeyValuePair<int, GameplayData> GameplayData in _IDtoGameplayData)
        {
            Debug.Log(GameplayData.Key + " :: " + GameplayData.Value.name);
        }

        Debug.Log("---------------------------------------------------------");
    }
#endif

    private void LoadGameplayDatas(string path)
    {
        GameplayData[] loadedGameplayDatas = Resources.LoadAll(path, typeof(GameplayData)).Cast<GameplayData>().ToArray();

        foreach (GameplayData loadedGameplayData in loadedGameplayDatas)
        {
            if (!loadedGameplayData.GameplayTag)
            {
                continue;
            }

            if (_IDtoGameplayData.ContainsKey(loadedGameplayData.GameplayTag.CompactTagId))
            {
                Debug.LogError("The GameplayData " + loadedGameplayData.Name + " has a duplicated ID!!!");
                continue;
            }

            _IDtoGameplayData.Add(loadedGameplayData.GameplayTag.CompactTagId, loadedGameplayData);
        }
    }

    public T GetGameplayData<T>(int ID) where T : GameplayData
    {
        if (!_IDtoGameplayData.ContainsKey(ID))
        {
            return null;
        }

        return _IDtoGameplayData[ID] as T;
    }
}
