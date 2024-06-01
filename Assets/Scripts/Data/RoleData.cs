using Assets.Scripts.Data;
using Assets.Scripts.Data.Tags;
using System;
using UnityEngine;

namespace Werewolf.Data
{
	public enum PrimaryRoleType
	{
		Villager,
		Werewolf
	};

	public enum SecondaryRoleType
	{
		None,
		Ambiguous,
		Lonely
	};

	[Serializable]
	public struct Priority
	{
		public int index;
		public RoleData alias;
	};

	[CreateAssetMenu(fileName = "Role", menuName = "ScriptableObjects/Roles/Role")]
	public class RoleData : GameplayData
	{
		[field: SerializeField]
		public PrimaryRoleType PrimaryType { get; private set; }

		[field: SerializeField]
		public SecondaryRoleType SecondaryType { get; private set; }

		[field: SerializeField]
		[field: TextArea(8, 20)]
		public string Instruction { get; private set; }

		// This allows to have more than once this role in a game
		[field: SerializeField]
		public bool CanHaveMultiples { get; private set; }

		// When CanHaveMultiples is false, there will be this exact number of this role at once in a game 
		[field: SerializeField]
		public int MandatoryCount { get; private set; }

		[field: SerializeField]
		public GameplayTag[] PlayerGroups { get; private set; }

		[field: SerializeField]
		public Priority[] NightPriorities { get; private set; }

		[field: SerializeField]
		public Priority[] DayPriorities { get; private set; }

		[field: SerializeField]
		public RoleBehavior Behavior { get; private set; }
	}
}