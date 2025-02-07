using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Utilities.GameplayData
{
	public class GameplayDataManager : KeptMonoSingleton<GameplayDataManager>
	{
		[SerializeField]
		private string[] _foldersToLoad;

		private readonly Dictionary<string, GameplayData> _guidToGameplayData = new();
		private readonly Dictionary<int, GameplayData> _hashCodeToGameplayData = new();

		[field: SerializeField]
		[field: ReadOnly]
		public bool IsReady { get; private set; }
#if UNITY_EDITOR
		[Header("Debug")]
		[SerializeField]
		private bool _logGameplayData = false;
#endif
		protected override void Awake()
		{
			base.Awake();

			_guidToGameplayData.Clear();
			_hashCodeToGameplayData.Clear();

			if (_foldersToLoad == null)
			{
				return;
			}

			foreach (string folder in _foldersToLoad)
			{
				LoadGameplayData(folder);
			}

			IsReady = true;
#if UNITY_EDITOR
			if (!_logGameplayData)
			{
				return;
			}

			LogGameplayData();
#endif
		}

		private void LoadGameplayData(string path)
		{
			GameplayData[] loadedGameplayDatas = Resources.LoadAll(path, typeof(GameplayData)).Cast<GameplayData>().ToArray();

			foreach (GameplayData loadedGameplayData in loadedGameplayDatas)
			{
				if (string.IsNullOrEmpty(loadedGameplayData.ID.Guid))
				{
					Debug.LogError("The GameplayData " + loadedGameplayData.name + " has no Guid!!!");
					continue;
				}

				if (_guidToGameplayData.ContainsKey(loadedGameplayData.ID.Guid))
				{
					Debug.LogError("The GameplayData " + loadedGameplayData.name + " has a duplicated Guid!!!");
					continue;
				}

				_guidToGameplayData.Add(loadedGameplayData.ID.Guid, loadedGameplayData);

				if (_hashCodeToGameplayData.ContainsKey(loadedGameplayData.ID.HashCode))
				{
					Debug.LogError("The GameplayData " + loadedGameplayData.name + " has a duplicated HashCode!!!");
					continue;
				}

				_hashCodeToGameplayData.Add(loadedGameplayData.ID.HashCode, loadedGameplayData);
			}
		}
#if UNITY_EDITOR
		private void LogGameplayData()
		{
			Debug.Log("----------------------GameplayData----------------------");

			foreach (KeyValuePair<string, GameplayData> GameplayData in _guidToGameplayData)
			{
				Debug.Log(GameplayData.Key + " :: " + GameplayData.Value.ID.HashCode + " :: " + GameplayData.Value.name);
			}

			Debug.Log("---------------------------------------------------------");
		}
#endif
		public T GetGameplayData<T>(string gameplayDataGuid) where T : GameplayData
		{
			if (!_guidToGameplayData.TryGetValue(gameplayDataGuid, out GameplayData gameplayData))
			{
				return null;
			}

			return gameplayData as T;
		}

		public T GetGameplayData<T>(int gameplayDataHashCode) where T : GameplayData
		{
			if (!_hashCodeToGameplayData.TryGetValue(gameplayDataHashCode, out GameplayData gameplayData))
			{
				return null;
			}

			return gameplayData as T;
		}

		public List<T> GetGameplayData<T>() where T : GameplayData
		{
			List<T> gameplayDatas = new();

			foreach (KeyValuePair<string, GameplayData> gameplayData in _guidToGameplayData)
			{
				if (gameplayData.Value is T castedGameplayData)
				{
					gameplayDatas.Add(castedGameplayData);
				}
			}

			return gameplayDatas;
		}
	}
}
