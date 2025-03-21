using Utilities.GameplayData;
using System;
using UnityEngine;
using UnityEngine.Localization;
using Werewolf.Gameplay.Role;

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

	[CreateAssetMenu(fileName = "RoleData", menuName = "ScriptableObjects/Roles/RoleData")]
	public class RoleData : GameplayData
	{
		[field: SerializeField]
		public LocalizedString NameSingular { get; private set; }

		[field: SerializeField]
		public LocalizedString NamePlural { get; private set; }

		[field: SerializeField]
		public LocalizedString Description { get; private set; }

		[field: SerializeField]
		[field: PreviewSprite]
		public Sprite Image { get; private set; }

		[field: SerializeField]
		[field: PreviewSprite]
		public Sprite SmallImage { get; private set; }

		[field: SerializeField]
		public PrimaryRoleType PrimaryType { get; private set; }

		[field: SerializeField]
		public SecondaryRoleType SecondaryType { get; private set; }

		// This allows to have more than once this role in a game
		[field: SerializeField]
		public bool CanHaveVariableAmount { get; private set; }

		// When CanHaveMultiples is false, there will be this exact number of this role at once in a game 
		[field: SerializeField]
		public int MandatoryAmount { get; private set; }

		[field: SerializeField]
		public PlayerGroupData[] PlayerGroups { get; private set; }

		[field: SerializeField]
		public Priority[] NightPriorities { get; private set; }

		[field: SerializeField]
		public RoleBehavior Behavior { get; private set; }
	}
}