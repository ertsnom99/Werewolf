using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using Werewolf.Data;
using static Werewolf.GameHistoryManager;

namespace Werewolf.UI
{
	[RequireComponent(typeof(ScrollRect))]
	public class GameHistory : MonoBehaviour
	{
		[Header("Game History Entries")]
		[SerializeField]
		private GameHistoryEntry _gameHistoryEntryPrefab;
		[SerializeField]
		private Transform _gameHistoryEntriesContainer;

		private readonly List<GameHistoryEntry> _gameHistoryEntries = new();

		private GameHistoryEntriesData _gameHistoryEntriesData;
		private PlayerGroupsData _playerGroupsData;

		private GameplayDatabaseManager _gameplayDatabaseManager;

		private ScrollRect _scrollRect;

		private void Start()
		{
			if (!_gameHistoryEntriesData)
			{
				Initialize();
			}
		}

		private void Initialize()
		{
			_gameHistoryEntriesData = GameHistoryManager.Instance.GameHistoryEntriesData;
			_playerGroupsData = PlayerGroupsManager.Instance.PlayerGroupsData;
			_gameplayDatabaseManager = GameplayDatabaseManager.Instance;

			_scrollRect = GetComponent<ScrollRect>();
		}

		public void DisplayGameHistory(GameHistorySave gameHistorySave)
		{
			if(!_gameHistoryEntriesData)
			{
				Initialize();
			}
			
			ClearGameHistoryEntries();

			GameHistoryEntry gameHistoryEntry;
			Sprite image;
			LocalizedString text;
			bool isOdd = true;

			foreach (GameHistorySaveEntry entry in gameHistorySave.Entries)
			{
				_gameHistoryEntriesData.GetGameHistoryEntryData(entry.EntryGameplayTagName, out GameHistoryEntryData gameHistoryEntryData);

				if (string.IsNullOrEmpty(entry.ImageOverrideGameplayTagName))
				{
					image = gameHistoryEntryData.Image;
				}
				else
				{
					image = null;

					string GameplayTagType = entry.ImageOverrideGameplayTagName.Split(new[] { '.' }, 2)[0];

					if (GameplayTagType == "Role")
					{
						image = _gameplayDatabaseManager.GetGameplayData<RoleData>(entry.ImageOverrideGameplayTagName).Image;
					}
					else if (GameplayTagType == "PlayerGroup")
					{
						_playerGroupsData.GetPlayerGroupData(entry.ImageOverrideGameplayTagName, out PlayerGroupData playerGroupsData);
						image = playerGroupsData.Image;
					}
				}

				text = new LocalizedString(gameHistoryEntryData.Text.TableReference, gameHistoryEntryData.Text.TableEntryReference);

				foreach (GameHistorySaveEntryVariable variable in entry.Variables)
				{
					switch(variable.Type)
					{
						case GameHistorySaveEntryVariableType.Player:
							text.Add(variable.Name, new StringVariable() { Value = variable.Data });
							break;
						case GameHistorySaveEntryVariableType.Players:
							text.Add(variable.Name, new StringListVariable() { Values = SplitData(variable.Data).ToList() });
							break;
						case GameHistorySaveEntryVariableType.RoleName:
							text.Add(variable.Name, _gameplayDatabaseManager.GetGameplayData<RoleData>(variable.Data).Name);
							break;
						case GameHistorySaveEntryVariableType.RoleNames:
							List<string> roleNames = SplitData(variable.Data).ToList();
							List<LocalizedString> localizedRoleNames = new();

							foreach(string roleName in roleNames)
							{
								localizedRoleNames.Add(_gameplayDatabaseManager.GetGameplayData<RoleData>(roleName).Name);
							}

							text.Add(variable.Name, new LocalizedStringListVariable() { Values = localizedRoleNames });
							break;
						case GameHistorySaveEntryVariableType.PlayerGroupeName:
							_playerGroupsData.GetPlayerGroupData(variable.Data, out PlayerGroupData playerGroupsData);
							text.Add(variable.Name, playerGroupsData.Name);
							break;
						case GameHistorySaveEntryVariableType.Bool:
							text.Add(variable.Name, new BoolVariable() { Value = bool.Parse(variable.Data) });
							break;
					}
				}

				gameHistoryEntry = Instantiate(_gameHistoryEntryPrefab, _gameHistoryEntriesContainer);
				gameHistoryEntry.Initialize(image, text, isOdd);
				_gameHistoryEntries.Add(gameHistoryEntry);

				isOdd = !isOdd;
			}

			_scrollRect.normalizedPosition = new Vector2(0, 1);
		}

		private void ClearGameHistoryEntries()
		{
			GameHistoryEntry gameHistoryEntry;

			while (_gameHistoryEntries.Count > 0)
			{
				gameHistoryEntry = _gameHistoryEntries[0];
				Destroy(gameHistoryEntry.gameObject);
				_gameHistoryEntries.RemoveAt(0);
			}
		}

		private void OnDisable()
		{
			ClearGameHistoryEntries();
		}
	}
}