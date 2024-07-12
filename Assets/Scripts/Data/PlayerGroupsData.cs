using Assets.Scripts.Data.Tags;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Werewolf.Data
{
	[Serializable]
	public struct PlayerGroupData
	{
		public GameplayTag GameplayTag;
		public LocalizedString Name;
		public bool HasMultiplePlayers;
		[field: PreviewSprite]
		public Sprite Image;
		[HideInInspector]
		public GameObject Leader;
	}

	[CreateAssetMenu(fileName = "PlayerGroupsData", menuName = "ScriptableObjects/PlayerGroupsData")]
	public class PlayerGroupsData : ScriptableObject
	{
		[field: SerializeField]
		public PlayerGroupData[] PlayerGroups { get; private set; }

		private Dictionary<int, int> _IDToPlayerGroup = new();
		private Dictionary<string, int> _gameplayTagNameToPlayerGroup = new();

		public void Init()
		{
			if (_IDToPlayerGroup.Count > 0)
			{
				return;
			}

			for (int i = 0; i < PlayerGroups.Length; i++)
			{
				_IDToPlayerGroup.Add(PlayerGroups[i].GameplayTag.CompactTagId, i);
			}

			if (_gameplayTagNameToPlayerGroup.Count > 0)
			{
				return;
			}

			for (int i = 0; i < PlayerGroups.Length; i++)
			{
				_gameplayTagNameToPlayerGroup.Add(PlayerGroups[i].GameplayTag.name, i);
			}
		}

		public bool GetPlayerGroupData(int ID, out PlayerGroupData playerGroupData)
		{
			if (!_IDToPlayerGroup.TryGetValue(ID, out int index))
			{
				Debug.LogError($"No PlayerGroupData has a gameplayTag with the ID {ID}");
				playerGroupData = default;
				return false;
			}

			playerGroupData = PlayerGroups[index];
			return true;
		}

		public bool GetPlayerGroupData(string gameplayTagName, out PlayerGroupData playerGroupData)
		{
			if (!_gameplayTagNameToPlayerGroup.TryGetValue(gameplayTagName, out int index))
			{
				Debug.LogError($"No PlayerGroupData has the gameplayTag {gameplayTagName}");
				playerGroupData = default;
				return false;
			}

			playerGroupData = PlayerGroups[index];
			return true;
		}

		public int GetPlayerGroupPriority(GameplayTag gameplayTag)
		{
			for (int i = 0; i < PlayerGroups.Length; i++)
			{
				if (PlayerGroups[i].GameplayTag == gameplayTag)
				{
					return (i + 1);
				}
			}

			Debug.LogError($"No PlayerGroupData has the gameplayTag {gameplayTag.name}");
			return -1;
		}
	}
}