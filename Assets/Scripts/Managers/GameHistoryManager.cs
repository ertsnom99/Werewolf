using Assets.Scripts.Data.Tags;
using Fusion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;

namespace Werewolf
{
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

	public class GameHistoryManager : NetworkBehaviourSingleton<GameHistoryManager>
	{
		[SerializeField]
		private GameplayTag[] _acceptedGameplayTagsForEntry;

		[SerializeField]
		private GameplayTag[] _acceptedGameplayTagsForImageOverride;

		[SerializeField]
		private GameHistoryEntriesData _gameHistoryEntriesData;

		private string _saveDirectoryPath;

		private GameHistorySave _gameHistorySave = new();

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

		public static string ConcatenatePlayersNickname(List<PlayerRef> players, NetworkDataManager networkDataManager)
		{
			if (players.Count <= 0)
			{
				return "";
			}

			StringBuilder stringBuilder = new(networkDataManager.PlayerInfos[players[0]].Nickname);

			for (int i = 1; i < players.Count; i++)
			{
				stringBuilder.Append(VARIABLE_DATA_SEPARATOR + networkDataManager.PlayerInfos[players[i]].Nickname);
			}

			return stringBuilder.ToString();
		}

		public void AddEntry(GameplayTag entryGameplayTag, GameHistorySaveEntryVariable[] variables, GameplayTag imageOverrideGameplayTag = null)
		{
			if (!entryGameplayTag)
			{
				Debug.LogError("Can't add a GameHistoryEntry without an entryGameplayTag");
				return;
			}

			if (!IsGameplayTagAccepted(entryGameplayTag, _acceptedGameplayTagsForEntry))
			{
				Debug.LogError($"{entryGameplayTag.name} is not a valid GameplayTag for the entryGameplayTag");
				return;
			}

			if (imageOverrideGameplayTag && !IsGameplayTagAccepted(imageOverrideGameplayTag, _acceptedGameplayTagsForImageOverride))
			{
				Debug.LogError($"{imageOverrideGameplayTag.name} is not a valid GameplayTag for the imageOverrideGameplayTag");
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

		public void SendGameHistoryToPlayers()
		{
			RPC_SendGameHistoryToPlayers(JsonUtility.ToJson(_gameHistorySave, true));
		}

		private void SaveGameHistoryToFile(string gameHistoryJson)
		{
			if (!Directory.Exists($"{_saveDirectoryPath}"))
			{
				Directory.CreateDirectory($"{_saveDirectoryPath}");
			}

			File.WriteAllText($"{_saveDirectoryPath}/{DateTime.Now:yyyy'_'MM'_'dd'_'HH'_'mm}{SAVE_FILE_EXTENSION}", gameHistoryJson);
		}

		#region RPC calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_SendGameHistoryToPlayers(string jsonGameHistory)
		{
			SaveGameHistoryToFile(jsonGameHistory);
		}
		#endregion
	}
}