using Assets.Scripts.Data;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameplayDatabaseManager : KeptMonoSingleton<GameplayDatabaseManager>
{
	[SerializeField]
	private string[] _foldersToLoad;

	private Dictionary<int, GameplayData> _IDtoGameplayData = new();
	private Dictionary<string, GameplayData> _gameplayTagNametoGameplayData = new();

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
		_gameplayTagNametoGameplayData.Clear();

		foreach (string folder in _foldersToLoad)
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
				Debug.LogError("The GameplayData " + loadedGameplayData.DebugName + " has a duplicated ID!!!");
				continue;
			}

			_IDtoGameplayData.Add(loadedGameplayData.GameplayTag.CompactTagId, loadedGameplayData);

			if (_gameplayTagNametoGameplayData.ContainsKey(loadedGameplayData.GameplayTag.name))
			{
				Debug.LogError("The GameplayData " + loadedGameplayData.DebugName + " has a duplicated GameplayTag name!!!");
				continue;
			}

			_gameplayTagNametoGameplayData.Add(loadedGameplayData.GameplayTag.name, loadedGameplayData);
		}
	}

	public T GetGameplayData<T>(int ID) where T : GameplayData
	{
		if (!_IDtoGameplayData.ContainsKey(ID))
		{
			Debug.LogError($"No GameplayData has the gameplayTag ID {ID}");
			return null;
		}

		return _IDtoGameplayData[ID] as T;
	}

	public T GetGameplayData<T>(string gameplayTagName) where T : GameplayData
	{
		if (!_gameplayTagNametoGameplayData.ContainsKey(gameplayTagName))
		{
			return null;
		}

		return _gameplayTagNametoGameplayData[gameplayTagName] as T;
	}

	public List<T> GetGameplayData<T>() where T : GameplayData
	{
		List<T> gameplayDatas = new List<T>();

		foreach (KeyValuePair<int, GameplayData> gameplayData in _IDtoGameplayData)
		{
			if (gameplayData.Value is T castedGameplayData)
			{
				gameplayDatas.Add(castedGameplayData);
			}
		}

		return gameplayDatas;
	}
}
