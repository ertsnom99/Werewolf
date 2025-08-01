using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Utilities.GameplayData;
using Werewolf.Data;
using Werewolf.Network;

namespace Werewolf.Gameplay.Role
{
	public abstract class RoleBehavior : MonoBehaviour
	{
		[field: SerializeField]
		[field: ReadOnly]
		public UniqueID RoleID { get; protected set; }

		[field: SerializeField]
		[field: ReadOnly]
		public PrimaryRoleType PrimaryRoleType { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public List<UniqueID> PlayerGroupIDs { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public List<Priority> NightPriorities { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public bool IsPrimaryBehavior { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public PlayerRef Player { get; private set; }

		public bool CanUsePower { get; set; }

		public virtual bool IsRolesSetupValid(NetworkArray<NetworkRoleSetup> mandatoryRoles, NetworkArray<NetworkRoleSetup> optionalRoles, GameplayDataManager gameplayDataManager, List<LocalizedString> warnings)
		{
			return true;
		}

		public void SetRoleID(UniqueID roleID)
		{
			RoleID = roleID;
		}

		public void SetPrimaryRoleType(PrimaryRoleType primaryRoleType)
		{
			PrimaryRoleType = primaryRoleType;
		}

		public void AddPlayerGroup(UniqueID playerGroupID)
		{
			if (PlayerGroupIDs == null)
			{
				PlayerGroupIDs = new() { playerGroupID };
			}
			else
			{
				PlayerGroupIDs.Add(playerGroupID);
			}
		}

		public virtual UniqueID[] GetCurrentPlayerGroupIDs()
		{
			return PlayerGroupIDs.ToArray();
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
			OnPlayerChanged();
		}

		public virtual void OnAddedReservedRoleID(int[] roleIDs, int index) { }

		public virtual void Initialize()
		{
			CanUsePower = true;
		}

		public abstract void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute);

		public abstract bool OnRoleCall(int priorityIndex, out bool isWakingUp);

		public virtual void GetTitlesOverride(int priorityIndex, ref Dictionary<PlayerRef, int> titlesOverride) { }

		public abstract void OnPlayerChanged();

		public abstract void OnRoleCallDisconnected();
	}
}