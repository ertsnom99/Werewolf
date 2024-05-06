using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public class ComedianBehavior : RoleBehavior
	{
		private GameManager _gameManager;

		private readonly int NEEDED_ROLE_COUNT = 3;

		public override void Init()
		{
			_gameManager = GameManager.Instance;
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles)
		{
			List<RoleData> selectedRoles = new();

			// Try to take the roles from the available roles first
			List<RoleSetupData> availableRolesCopy = new(availableRoles);

			while (availableRolesCopy.Count > 0 && selectedRoles.Count < NEEDED_ROLE_COUNT)
			{
				int roleSetupIndex = Random.Range(0, availableRolesCopy.Count);

				if (!CanTakeRolesFromSetup(availableRolesCopy[roleSetupIndex], selectedRoles))
				{
					availableRolesCopy.RemoveAt(roleSetupIndex);
					continue;
				}

				SelectRolesFromRoleSetup(availableRolesCopy[roleSetupIndex], ref selectedRoles);

				availableRoles.Remove(availableRolesCopy[roleSetupIndex]);
				availableRolesCopy.RemoveAt(roleSetupIndex);
			}

			// Roles taken from the available roles need to have their behavior instanciated
			_gameManager.PrepareRoleBehaviors(selectedRoles.ToArray(), ref rolesToDistribute, ref availableRoles);

			// Take the rest from the roles to distribute
			List<RoleData> rolesToDistributeCopy = new(rolesToDistribute);

			while (rolesToDistributeCopy.Count > 0 && selectedRoles.Count < NEEDED_ROLE_COUNT)
			{
				int roleIndex = Random.Range(0, rolesToDistributeCopy.Count);

				if (!IsRoleValid(rolesToDistributeCopy[roleIndex], selectedRoles))
				{
					rolesToDistributeCopy.RemoveAt(roleIndex);
					continue;
				}

				selectedRoles.Add(rolesToDistributeCopy[roleIndex]);

				rolesToDistribute.Remove(rolesToDistributeCopy[roleIndex]);
				rolesToDistributeCopy.RemoveAt(roleIndex);
			}

			if (selectedRoles.Count < NEEDED_ROLE_COUNT)
			{
				Debug.LogError("The comedian couldn't find enough roles to set aside!!!");
			}

			if (selectedRoles.Count <= 0)
			{
				return;
			}

			_gameManager.ReserveRoles(this, selectedRoles.ToArray(), true);
		}

		private bool CanTakeRolesFromSetup(RoleSetupData roleSetup, List<RoleData> selectedRoles)
		{
			if (NEEDED_ROLE_COUNT - selectedRoles.Count < roleSetup.UseCount)
			{
				return false;
			}

			List<RoleData> validRoles = new();

			foreach (RoleData role in roleSetup.Pool)
			{
				if (!validRoles.Contains(role) && IsRoleValid(role, selectedRoles))
				{
					validRoles.Add(role);

					if (validRoles.Count >= roleSetup.UseCount)
					{
						return true;
					}
				}
			}

			return false;
		}

		private bool IsRoleValid(RoleData role, List<RoleData> selectedRoles)
		{
			return !selectedRoles.Contains(role)
					&& role.PrimaryType == PrimaryRoleType.Villager
					&& role.SecondaryType != SecondaryRoleType.None
					&& role.Behavior
					&& !role.Behavior.GetType().Equals(GetType());
		}

		private void SelectRolesFromRoleSetup(RoleSetupData roleSetup, ref List<RoleData> selectedRoles)
		{
			int rolesSelectedCount = 0;
			int indexOffset = Random.Range(0, roleSetup.UseCount);

			for (int i = 0; i < roleSetup.Pool.Length; i++)
			{
				int adjustedIndex = (i + indexOffset) % roleSetup.Pool.Length;

				if (IsRoleValid(roleSetup.Pool[adjustedIndex], selectedRoles))
				{
					selectedRoles.Add(roleSetup.Pool[adjustedIndex]);
					rolesSelectedCount++;
				}

				if (rolesSelectedCount >= roleSetup.UseCount)
				{
					return;
				}
			}
		}

		public override bool OnRoleCall()
		{
			return true;
		}

		public override void OnRoleCallDisconnected() { }
	}
}