using System;
using UnityEngine;

namespace Werewolf.Data
{
	[Serializable]
	public struct PlayerGroupData
	{
		public int Index;
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

		public PlayerGroupData GetPlayerGroupData(int index)
		{
			foreach(PlayerGroupData data in Datas)
			{
				if (data.Index == index)
				{
					return data;
				}
			}

			return new();
		}
	}
}