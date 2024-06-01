using Assets.Scripts.Data.Tags;
using System;
using UnityEngine;

namespace Werewolf.Data
{
	[Serializable]
	public struct PlayerGroupData
	{
		public GameplayTag GameplayTag;
		public string Name;
		[field: PreviewSprite]
		public Sprite Image;
		[HideInInspector]
		public GameObject Leader;
	}

	[CreateAssetMenu(fileName = "PlayerGroupsData", menuName = "ScriptableObjects/PlayerGroupsData")]
	public class PlayerGroupsData : ScriptableObject
	{
		[field: SerializeField]
		public PlayerGroupData[] Datas { get; private set; }

		public PlayerGroupData GetPlayerGroup(int ID)
		{
			foreach(PlayerGroupData data in Datas)
			{
				if (data.GameplayTag.CompactTagId == ID)
				{
					return data;
				}
			}

			return new();
		}

		public int GetPlayerGroupPriority(GameplayTag gameplayTag)
		{
			for (int i = 0; i < Datas.Length; i++)
			{
				if (Datas[i].GameplayTag == gameplayTag)
				{
					return (i + 1);
				}
			}

			return -1;
		}
	}
}