using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Utilities.GameplayData
{
	public class GameplayDataManager : KeptMonoSingleton<GameplayDataManager>
	{
		[Header("Settings")]
		[SerializeField]
		private string[] _foldersToLoad;

		[SerializeField]
		private int[] _prohibitedHashCodes;

		[SerializeField]
		private bool _buildGuidDictionary;

		[SerializeField]
		private bool _buildHashCodeDictionary;

		[field: SerializeField]
		[field: ReadOnly]
		public bool IsReady { get; private set; }
#if UNITY_EDITOR
		[Header("Debug")]
		[SerializeField]
		private bool _logGameplayData = false;

		public struct SanityIssue
		{
			public string gameplayDataName;
			public string issue;
		}
#endif
		private readonly Dictionary<string, GameplayData> _guidToGameplayData = new();
		private readonly Dictionary<int, GameplayData> _hashCodeToGameplayData = new();

		protected override void Awake()
		{
			base.Awake();

			_guidToGameplayData.Clear();
			_hashCodeToGameplayData.Clear();

			if (_foldersToLoad == null || (!_buildGuidDictionary && !_buildHashCodeDictionary))
			{
				IsReady = true;
				return;
			}

			foreach (string folder in _foldersToLoad)
			{
				LoadGameplayData(folder);
			}

			IsReady = true;
#if UNITY_EDITOR
			if (_logGameplayData)
			{
				LogGameplayData();
			}
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

				if (_prohibitedHashCodes.Contains(loadedGameplayData.ID.HashCode))
				{
					Debug.LogError("The GameplayData " + loadedGameplayData.name + " has a prohibited HashCode!!!");
					continue;
				}

				if (_buildGuidDictionary)
				{
					if (_guidToGameplayData.ContainsKey(loadedGameplayData.ID.Guid))
					{
						Debug.LogError("The GameplayData " + loadedGameplayData.name + " has a duplicated Guid!!!");
					}
					else
					{
						_guidToGameplayData.Add(loadedGameplayData.ID.Guid, loadedGameplayData);
					}
				}

				if (_buildHashCodeDictionary)
				{
					if (_hashCodeToGameplayData.ContainsKey(loadedGameplayData.ID.HashCode))
					{
						Debug.LogError("The GameplayData " + loadedGameplayData.name + " has a duplicated HashCode!!!");
					}
					else
					{
						_hashCodeToGameplayData.Add(loadedGameplayData.ID.HashCode, loadedGameplayData);
					}
				}
			}
		}
#if UNITY_EDITOR
		private void LogGameplayData()
		{
			if (_buildGuidDictionary)
			{
				Debug.Log("----------------------GameplayData By Guid----------------------");

				foreach (KeyValuePair<string, GameplayData> GameplayData in _guidToGameplayData)
				{
					Debug.Log(GameplayData.Key + " :: " + GameplayData.Value.name);
				}

				Debug.Log("----------------------------------------------------------------");
			}

			if (_buildHashCodeDictionary)
			{
				Debug.Log("----------------------GameplayData By HashCode----------------------");

				foreach (KeyValuePair<int, GameplayData> GameplayData in _hashCodeToGameplayData)
				{
					Debug.Log(GameplayData.Key + " :: " + GameplayData.Value.name);
				}

				Debug.Log("--------------------------------------------------------------------");
			}
		}

		public SanityIssue[] CheckGameplayDataSanity()
		{
			List<string> guid = new();
			List<int> hashCode = new();
			List<SanityIssue> issues = new();

			foreach (string folder in _foldersToLoad)
			{
				GameplayData[] loadedGameplayDatas = Resources.LoadAll(folder, typeof(GameplayData)).Cast<GameplayData>().ToArray();

				foreach (GameplayData loadedGameplayData in loadedGameplayDatas)
				{
					if (string.IsNullOrEmpty(loadedGameplayData.ID.Guid))
					{
						issues.Add(new() { gameplayDataName = loadedGameplayData.name, issue = "Has no Guid" });
						continue;
					}

					if (_prohibitedHashCodes.Contains(loadedGameplayData.ID.HashCode))
					{
						issues.Add(new() { gameplayDataName = loadedGameplayData.name, issue = "Has a prohibited HashCode" });
						continue;
					}

					if (guid.Contains(loadedGameplayData.ID.Guid))
					{
						issues.Add(new() { gameplayDataName = loadedGameplayData.name, issue = "Has a duplicated Guid" });
						continue;
					}
					else
					{
						guid.Add(loadedGameplayData.ID.Guid);
					}

					if (hashCode.Contains(loadedGameplayData.ID.HashCode))
					{
						issues.Add(new() { gameplayDataName = loadedGameplayData.name, issue = "Has a duplicated HashCode" });
						continue;
					}
					else
					{
						hashCode.Add(loadedGameplayData.ID.HashCode);
					}
				}
			}

			return issues.ToArray();
		}
#endif
		public bool TryGetGameplayData<T>(string gameplayDataGuid, out T outGameplayData) where T : GameplayData
		{
			if (!_guidToGameplayData.TryGetValue(gameplayDataGuid, out GameplayData gameplayData) || gameplayData is not T)
			{
				outGameplayData = null;
				return false;
			}

			outGameplayData = gameplayData as T;
			return true;
		}

		public bool TryGetGameplayData<T>(int gameplayDataHashCode, out T outGameplayData) where T : GameplayData
		{
			if (!_hashCodeToGameplayData.TryGetValue(gameplayDataHashCode, out GameplayData gameplayData) || gameplayData is not T)
			{
				outGameplayData = null;
				return false;
			}

			outGameplayData = gameplayData as T;
			return true;
		}

		public List<T> TryGetGameplayData<T>() where T : GameplayData
		{
			List<T> gameplayDatas = new();

			if (_buildGuidDictionary)
			{
				foreach (KeyValuePair<string, GameplayData> gameplayData in _guidToGameplayData)
				{
					if (gameplayData.Value is T castedGameplayData)
					{
						gameplayDatas.Add(castedGameplayData);
					}
				}
			}
			else if (_buildHashCodeDictionary)
			{
				foreach (KeyValuePair<int, GameplayData> gameplayData in _hashCodeToGameplayData)
				{
					if (gameplayData.Value is T castedGameplayData)
					{
						gameplayDatas.Add(castedGameplayData);
					}
				}
			}

			return gameplayDatas;
		}
	}
}
