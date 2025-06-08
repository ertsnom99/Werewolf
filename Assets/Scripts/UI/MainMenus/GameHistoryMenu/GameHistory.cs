using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using Utilities.GameplayData;
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

		private GameplayDataManager _gameplayDataManager;

		private void Start()
		{
			if (!_gameplayDataManager)
			{
				Initialize();
			}
		}

		private void Initialize()
		{
			_gameplayDataManager = GameplayDataManager.Instance;

			_scrollRect = GetComponent<ScrollRect>();
		}

		public void DisplayGameHistory(GameHistorySave gameHistorySave)
		{
			if(!_gameplayDataManager)
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
				if (!_gameplayDataManager.TryGetGameplayData(entry.EntryID, out GameHistoryEntryData gameHistoryEntryData))
				{
					Debug.LogError($"Could not find the game history entry {entry.EntryID}");
					continue;
				}

				if (string.IsNullOrEmpty(entry.ImageOverrideID))
				{
					image = gameHistoryEntryData.Image;
				}
				else
				{
					image = null;

					int imageOverrideID = int.Parse(entry.ImageOverrideID);

					if (_gameplayDataManager.TryGetGameplayData(imageOverrideID, out RoleData roleData))
					{
						image = roleData.SmallImage;
					}
					else if (_gameplayDataManager.TryGetGameplayData(imageOverrideID, out PlayerGroupData playerGroupData))
					{
						image = playerGroupData.SmallImage;
					}
					else
					{
						Debug.LogError($"imageOverrideID {imageOverrideID} of history entry {entry.EntryID} was neither a valid {nameof(RoleData)} or a {nameof(PlayerGroupData)}");
						continue;
					}
				}

				text = new LocalizedString(gameHistoryEntryData.Text.TableReference, gameHistoryEntryData.Text.TableEntryReference);

				foreach (GameHistorySaveEntryVariable variable in entry.Variables)
				{
					switch(variable.Type)
					{
						case GameHistorySaveEntryVariableType.Player:
							text.Add(variable.Name, new StringVariable() { Value = variable.Data });//TEMP<---Make it without creating new variables(might not work)
							break;
						case GameHistorySaveEntryVariableType.Players:
							text.Add(variable.Name, new StringListVariable() { Values = SplitData(variable.Data).ToList() });
							break;
						case GameHistorySaveEntryVariableType.RoleName:
						{
							if (_gameplayDataManager.TryGetGameplayData(int.Parse(variable.Data), out RoleData roleData))
							{
								text.Add(variable.Name, roleData.NameSingular);
							}
							else
							{
								Debug.LogError($"variable.Data {variable.Data} of history entry {entry.EntryID} was not a valid {nameof(RoleData)}");
							}
							break;
						}
						case GameHistorySaveEntryVariableType.RoleNames:
						{
							List<string> roleNames = SplitData(variable.Data).ToList();
							List<LocalizedString> localizedRoleNames = new();

							foreach (string roleName in roleNames)
							{
								if (_gameplayDataManager.TryGetGameplayData(int.Parse(roleName), out RoleData roleData))
								{
									localizedRoleNames.Add(roleData.NameSingular);
								}
								else
								{
									Debug.LogError($"variable.Data {roleName} of history entry {entry.EntryID} was not a valid {nameof(RoleData)}");
								}
							}

							text.Add(variable.Name, new LocalizedStringListVariable() { Values = localizedRoleNames });
							break;
						}
						case GameHistorySaveEntryVariableType.PlayerGroupeName:
						{
							if (_gameplayDataManager.TryGetGameplayData(int.Parse(variable.Data), out PlayerGroupData playerGroupData))
							{
								text.Add(variable.Name, playerGroupData.Name);
							}
							else
							{
								Debug.LogError($"variable.Data {variable.Data} of history entry {entry.EntryID} was not a valid {nameof(PlayerGroupData)}");
							}
							break;
						}
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
	}
}