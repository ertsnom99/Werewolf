using Assets.Scripts.Data.Tags;
using Fusion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;
using Werewolf.UI;

namespace Werewolf.Managers
{
	public class GameHistoryManager : MonoSingleton<GameHistoryManager>
	{
		[SerializeField]
		private GameplayTag[] _acceptedGameplayTagsForEntry;

		[SerializeField]
		private GameplayTag[] _acceptedGameplayTagsForImageOverride;

		private string _saveDirectoryPath;

		private readonly GameHistorySave _gameHistorySave = new();

		[Serializable]
		public class GameHistorySave
		{
			public List<GameHistorySaveEntry> Entries = new();
		}

		[Serializable]
		public struct GameHistorySaveEntry
		{
			public string EntryGameplayTagName;
			public string ImageOverrideGameplayTagName;
			public GameHistorySaveEntryVariable[] Variables;
		}

		[Serializable]
		public struct GameHistorySaveEntryVariable
		{
			public string Name;
			public string Data;
			public GameHistorySaveEntryVariableType Type;
		}

		public enum GameHistorySaveEntryVariableType
		{
			Player,
			Players,
			RoleName,
			RoleNames,
			PlayerGroupeName,
			Bool
		}

		private const char VARIABLE_DATA_SEPARATOR = '|';

		private const string SAVE_FOLDER = "GameHistory";
		private const string SAVE_FILE_EXTENSION = ".json";

		protected override void Awake()
		{
			base.Awake();

			DontDestroyOnLoad(gameObject);
			_saveDirectoryPath = $"{Application.persistentDataPath}/{SAVE_FOLDER}";
		}

		public void ClearEntries()
		{
			_gameHistorySave.Entries.Clear();
		}

		public static string ConcatenatePlayersNickname(PlayerRef[] players, NetworkDataManager networkDataManager)
		{
			if (players.Length <= 0)
			{
				return "";
			}

			StringBuilder stringBuilder = new(networkDataManager.PlayerInfos[players[0]].Nickname);

			for (int i = 1; i < players.Length; i++)
			{
				stringBuilder.Append(VARIABLE_DATA_SEPARATOR + networkDataManager.PlayerInfos[players[i]].Nickname);
			}

			return stringBuilder.ToString();
		}

		public static string ConcatenateRolesName(List<RoleData> roles)
		{
			if (roles.Count <= 0)
			{
				return "";
			}

			StringBuilder stringBuilder = new(roles[0].GameplayTag.name);

			for (int i = 1; i < roles.Count; i++)
			{
				stringBuilder.Append(VARIABLE_DATA_SEPARATOR + roles[i].GameplayTag.name);
			}

			return stringBuilder.ToString();
		}

		public static string[] SplitData(string data)
		{
			return data.Split(VARIABLE_DATA_SEPARATOR, StringSplitOptions.RemoveEmptyEntries);
		}

		public void AddEntry(GameplayTag entryGameplayTag, GameHistorySaveEntryVariable[] variables, GameplayTag imageOverrideGameplayTag = null)
		{
			if (!entryGameplayTag)
			{
				Debug.LogError($"Can't add a {nameof(GameHistoryEntry)} without an {nameof(GameplayTag)}");
				return;
			}

			if (!IsGameplayTagAccepted(entryGameplayTag, _acceptedGameplayTagsForEntry))
			{
				Debug.LogError($"{entryGameplayTag.name} is not a valid {nameof(GameplayTag)} to add an entry");
				return;
			}

			if (imageOverrideGameplayTag && !IsGameplayTagAccepted(imageOverrideGameplayTag, _acceptedGameplayTagsForImageOverride))
			{
				Debug.LogError($"{imageOverrideGameplayTag.name} is not a valid {nameof(GameplayTag)} for the image override");
				return;
			}

			_gameHistorySave.Entries.Add(new() { EntryGameplayTagName = entryGameplayTag.name,
																ImageOverrideGameplayTagName = imageOverrideGameplayTag ? imageOverrideGameplayTag.name : "",
																Variables = variables });
		}

		private bool IsGameplayTagAccepted(GameplayTag gameplayTag, GameplayTag[] acceptedGameplayTags)
		{
			foreach (GameplayTag acceptedGameplayTag in acceptedGameplayTags)
			{
				if (gameplayTag.IsInCategory(acceptedGameplayTag))
				{
					return true;
				}
			}

			return false;
		}

		public string GetGameHistoryJson()
		{
			return JsonUtility.ToJson(_gameHistorySave);
		}

		public void SaveGameHistoryToFile(string fileName, string gameHistoryJson)
		{
			if (!Directory.Exists($"{_saveDirectoryPath}"))
			{
				Directory.CreateDirectory($"{_saveDirectoryPath}");
			}

			string path = $"{_saveDirectoryPath}/{fileName}{SAVE_FILE_EXTENSION}";

			if (!File.Exists(path))
			{
				File.WriteAllText(path, gameHistoryJson);
			}
		}

		public string[] GetSavedGameHistoryFilePaths()
		{
			if (!Directory.Exists($"{_saveDirectoryPath}"))
			{
				return new string[0];
			}

			return Directory.GetFiles(_saveDirectoryPath);
		}

		public bool LoadGameHistorySaveFromFile(string filePath, out GameHistorySave gameHistorySave)
		{
			if (!File.Exists(filePath))
			{
				Debug.LogError($"There is no file {filePath}");

				gameHistorySave = null;
				return false;
			}

			string gameHistoryJson = File.ReadAllText(filePath);
			return LoadGameHistorySaveFromJson(gameHistoryJson, out gameHistorySave);
		}

		public bool LoadGameHistorySaveFromJson(string gameHistoryJson, out GameHistorySave gameHistorySave)
		{
			try
			{
				gameHistorySave = JsonUtility.FromJson<GameHistorySave>(gameHistoryJson);
			}
			catch (Exception)
			{
				Debug.LogError($"Couldn't convert the json to {nameof(GameHistorySave)}");

				gameHistorySave = null;
				return false;
			}

			return gameHistorySave != null;
		}

		public bool DeleteGameHistory(string filePath)
		{
			if (!File.Exists(filePath))
			{
				Debug.LogError($"There is no file {filePath}");
				return false;
			}

			File.Delete(filePath);
			return true;
		}

		public bool DeleteAllGameHistory()
		{
			string[] filePaths = GetSavedGameHistoryFilePaths();
			
			bool deletedAll = true;

			foreach (string filePath in filePaths)
			{
				if (DeleteGameHistory(filePath))
				{
					continue;
				}

				deletedAll = false;
			}

			return deletedAll;
		}
	}
}