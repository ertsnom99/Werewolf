using Assets.Scripts.Data.Tags;
using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf.Gameplay.Role
{
	public abstract class RoleBehavior : MonoBehaviour
	{
		[field: SerializeField]
		[field: ReadOnly]
		public GameplayTag RoleGameplayTag { get; protected set; }

		[field: SerializeField]
		[field: ReadOnly]
		public PrimaryRoleType PrimaryRoleType { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public List<GameplayTag> PlayerGroups { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public List<Priority> NightPriorities { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public bool IsPrimaryBehavior { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public PlayerRef Player { get; private set; }

		public void SetRoleGameplayTag(GameplayTag roleGameplayTag)
		{
			RoleGameplayTag = roleGameplayTag;
		}

		public void SetPrimaryRoleType(PrimaryRoleType primaryRoleType)
		{
			PrimaryRoleType = primaryRoleType;
		}

		public void AddPlayerGroup(GameplayTag playerGroup)
		{
			if (PlayerGroups == null)
			{
				PlayerGroups = new() { playerGroup };
			}
			else
			{
				PlayerGroups.Add(playerGroup);
			}
		}

		public virtual GameplayTag[] GetCurrentPlayerGroups()
		{
			return PlayerGroups.ToArray();
		}

		public void AddNightPriority(Priority nightPriority)
		{
			if (NightPriorities == null)
			{
				NightPriorities = new() { nightPriority };
			}
			else
			{
				NightPriorities.Add(nightPriority);
			}
		}

		public int[] GetNightPrioritiesIndexes()
		{
			List<int> nightPrioritiesIndexes = new();

			foreach (Priority nightPrioritie in NightPriorities)
			{
				nightPrioritiesIndexes.Add(nightPrioritie.index);
			}

			return nightPrioritiesIndexes.ToArray();
		}

		public void SetIsPrimaryBehavior(bool isPrimaryBehavior)
		{
			IsPrimaryBehavior = isPrimaryBehavior;
		}

		public void SetPlayer(PlayerRef player)
		{
			Player = player;
		}

		public abstract void Initialize();

		public abstract void OnSelectedToDistribute(List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles, List<RoleData> rolesToDistribute);

		public abstract bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp);

		public virtual void GetTitlesOverride(int priorityIndex, ref Dictionary<PlayerRef, int> titlesOverride) { }

		public abstract void ReInitialize();

		public abstract void OnRoleCallDisconnected();
	}
}