using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public abstract class RoleBehavior : MonoBehaviour
	{
		[field: SerializeField]
		[field: ReadOnly]
		public PrimaryRoleType PrimaryRoleType { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public List<int> PlayerGroupIndexes { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public List<Priority> NightPriorities { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public bool IsPrimaryBehavior { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public PlayerRef Player { get; private set; }

		protected bool _timedOut = false;

		public void AddPlayerGroupIndex(int playerGroupIndex)
		{
			if (PlayerGroupIndexes == null)
			{
				PlayerGroupIndexes = new() { playerGroupIndex };
			}
			else
			{
				PlayerGroupIndexes.Add(playerGroupIndex);
			}
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

		public void SetPrimaryRoleType(PrimaryRoleType primaryRoleType)
		{
			PrimaryRoleType = primaryRoleType;
		}

		public void SetPlayer(PlayerRef player)
		{
			Player = player;
		}

		public abstract void Init();

		public abstract void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles);

		public virtual int[] GetCurrentPlayerGroups()
		{
			return PlayerGroupIndexes.ToArray();
		}

		public abstract bool OnRoleCall();

		public void SetTimedOut(bool timedOut)
		{
			_timedOut = timedOut;
		}

		public abstract void OnRoleTimeOut();
	}
}