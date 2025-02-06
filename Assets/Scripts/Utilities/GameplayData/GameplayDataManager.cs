using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Utilities.GameplayData
{
	public class GameplayDataManager : KeptMonoSingleton<GameplayDataManager>
	{
		[SerializeField]
		private string[] _foldersToLoad;

		private readonly Dictionary<string, GameplayData> _IDtoGameplayData = new();

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

			_IDtoGameplayData.Clear();

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
				if (string.IsNullOrEmpty(loadedGameplayData.ID.Value))
				{
					Debug.LogError("The GameplayData " + loadedGameplayData.name + " has no ID!!!");
					continue;
				}

				if (_IDtoGameplayData.ContainsKey(loadedGameplayData.ID.Value))
				{
					Debug.LogError("The GameplayData " + loadedGameplayData.name + " has a duplicated ID!!!");
					continue;
				}

				_IDtoGameplayData.Add(loadedGameplayData.ID.Value, loadedGameplayData);
			}
		}
#if UNITY_EDITOR
		private void LogGameplayData()
		{
			Debug.Log("----------------------GameplayData----------------------");

			foreach (KeyValuePair<string, GameplayData> GameplayData in _IDtoGameplayData)
			{
				Debug.Log(GameplayData.Key + " :: " + GameplayData.Value.ID.Value);
			}

			Debug.Log("---------------------------------------------------------");
		}
#endif
		public T GetGameplayData<T>(string gameplayDataID) where T : GameplayData
		{
			if (!_IDtoGameplayData.TryGetValue(gameplayDataID, out GameplayData gameplayData))
			{
				return null;
			}

			return gameplayData as T;
		}

		public List<T> GetGameplayData<T>() where T : GameplayData
		{
			List<T> gameplayDatas = new();

			foreach (KeyValuePair<string, GameplayData> gameplayData in _IDtoGameplayData)
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
