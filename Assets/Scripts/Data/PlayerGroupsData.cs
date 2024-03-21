using System;
using UnityEngine;

namespace Werewolf.Data
{
	[Serializable]
	public struct PlayerGroupData
	{
		public int Index;
		public string Name;
		[HideInInspector]
		public GameObject Leader;
	}

	[CreateAssetMenu(fileName = "PlayerGroupsData", menuName = "ScriptableObjects/PlayerGroupsData")]
	public class PlayerGroupsData : ScriptableObject
	{
		[field: SerializeField]
		public PlayerGroupData[] Datas { get; private set; }
	}
}