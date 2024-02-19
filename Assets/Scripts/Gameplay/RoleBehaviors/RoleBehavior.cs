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
		public PlayerRef Player { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public List<int> NightPriorities { get; private set; }

		[field: SerializeField]
		[field: ReadOnly]
		public bool IsPrimaryBehavior { get; private set; }

		protected bool _timedOut = false;

		public void SetPlayer(PlayerRef player)
		{
			Player = player;
		}

		public void AddNightPriority(int nightPriority)
		{
			if (NightPriorities == null)
			{
				NightPriorities = new List<int> { nightPriority };
			}
			else
			{
				NightPriorities.Add(nightPriority);
			}
		}

		public void SetIsPrimaryBehavior(bool isPrimaryBehavior)
		{
			IsPrimaryBehavior = isPrimaryBehavior;
		}

		public abstract void Init();

		public abstract void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles);

		public void SetTimedOut(bool timedOut)
		{
			_timedOut = timedOut;
		}

		public abstract bool OnRoleCall();

		public abstract void OnRoleTimeOut();
	}
}