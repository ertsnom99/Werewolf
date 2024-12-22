using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using Werewolf.Data;
using static Werewolf.Managers.GameHistoryManager;

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

		private ScrollRect _scrollRect;

		private GameplayDatabaseManager _gameplayDatabaseManager;

		private void Start()
		{
			if (!_gameplayDatabaseManager)
			{
				Initialize();
			}
		}

		private void Initialize()
		{
			_gameplayDatabaseManager = GameplayDatabaseManager.Instance;

			_scrollRect = GetComponent<ScrollRect>();
		}

		public void DisplayGameHistory(GameHistorySave gameHistorySave)
		{
			if(!_gameplayDatabaseManager)
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
				GameHistoryEntryData gameHistoryEntryData = _gameplayDatabaseManager.GetGameplayData<GameHistoryEntryData>(entry.EntryGameplayTagName);

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
						PlayerGroupData playerGroupData = _gameplayDatabaseManager.GetGameplayData<PlayerGroupData>(entry.ImageOverrideGameplayTagName);
						image = playerGroupData.Image;
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
							text.Add(variable.Name, _gameplayDatabaseManager.GetGameplayData<RoleData>(variable.Data).NameSingular);
							break;
						case GameHistorySaveEntryVariableType.RoleNames:
							List<string> roleNames = SplitData(variable.Data).ToList();
							List<LocalizedString> localizedRoleNames = new();

							foreach(string roleName in roleNames)
							{
								localizedRoleNames.Add(_gameplayDatabaseManager.GetGameplayData<RoleData>(roleName).NameSingular);
							}

							text.Add(variable.Name, new LocalizedStringListVariable() { Values = localizedRoleNames });
							break;
						case GameHistorySaveEntryVariableType.PlayerGroupeName:
							PlayerGroupData playerGroupData = _gameplayDatabaseManager.GetGameplayData<PlayerGroupData>(variable.Data);
							text.Add(variable.Name, playerGroupData.Name);
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

		public void ClearGameHistoryEntries()
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