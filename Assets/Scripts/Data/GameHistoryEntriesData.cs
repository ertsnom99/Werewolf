using Assets.Scripts.Data.Tags;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Werewolf.Data
{
	[Serializable]
	public struct GameHistoryEntryData
	{
		public GameplayTag GameplayTag;
		[field: PreviewSprite]
		public Sprite Image;
		public LocalizedString Text;
	}

	[CreateAssetMenu(fileName = "GameHistoryEntriesData", menuName = "ScriptableObjects/GameHistoryEntriesData")]
	public class GameHistoryEntriesData : ScriptableObject
	{
		[SerializeField]
		public GameHistoryEntryData[] _gameHistoryEntries;

		private Dictionary<string, int> _gameplayTagNameToGameHistoryEntry = new();

		public void Init()
		{
			if (_gameplayTagNameToGameHistoryEntry.Count > 0)
			{
				return;
			}

			for (int i = 0; i < _gameHistoryEntries.Length; i++)
			{
				_gameplayTagNameToGameHistoryEntry.Add(_gameHistoryEntries[i].GameplayTag.name, i);
			}
		}

		public bool GetGameHistoryEntryData(string gameplayTagName, out GameHistoryEntryData gameHistoryEntryData)
		{
			if (!_gameplayTagNameToGameHistoryEntry.TryGetValue(gameplayTagName, out int index))
			{
				Debug.LogError($"No GameHistoryEntryData has the gameplayTag {gameplayTagName}");
				gameHistoryEntryData = default;
				return false;
			}

			gameHistoryEntryData = _gameHistoryEntries[index];
			return true;
		}
	}
}