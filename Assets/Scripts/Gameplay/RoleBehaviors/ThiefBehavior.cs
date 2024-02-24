using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public class ThiefBehavior : RoleBehavior
	{
		[SerializeField]
		private RoleData[] _rolesToAdd;

		private GameManager.IndexedReservedRoles _reservedRoles;
		private bool _reservedOnlyWerewolfs;

		private GameManager _gameManager;

		public override void Init()
		{
			_gameManager = GameManager.Instance;

			_gameManager.OnPreRoleDistribution += OnPreRoleDistribution;
			_gameManager.OnPostRoleDistribution += OnPostRoleDistribution;
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

		private void OnPreRoleDistribution()
		{
			_gameManager.AddRolesToDistribute(_rolesToAdd);
		}

		private void OnPostRoleDistribution()
		{
			List<RoleData> roles = new();

			for (int i = 0; i < _rolesToAdd.Length; i++)
			{
				if (_gameManager.RolesToDistribute.Count <= 0)
				{
					Debug.LogError("The thief couldn't find enough roles to set aside!!!");
					break;
				}

				int randomIndex = Random.Range(0, _gameManager.RolesToDistribute.Count);
				roles.Add(_gameManager.RolesToDistribute[randomIndex]);

				_gameManager.RemoveRoleToDistribute(_gameManager.RolesToDistribute[randomIndex]);
			}

			_gameManager.ReserveRoles(this, roles.ToArray(), false);
		}

		public override bool OnRoleCall()
		{
			_reservedRoles = _gameManager.GetReservedRoles(this);

			if (_reservedRoles.Roles == null || _reservedRoles.Roles.Length < 0)
			{
				return false;
			}

			_reservedOnlyWerewolfs = true;

			foreach (RoleData role in _reservedRoles.Roles)
			{
				if (role.PrimaryType != PrimaryRoleType.Werewolf)
				{
					_reservedOnlyWerewolfs = false;
					break;
				}
			}

			return _gameManager.AskClientToChooseReservedRole(this, _reservedOnlyWerewolfs, OnRoleSelected);
		}

		private void OnRoleSelected(int choiceIndex)
		{
			if (_timedOut)
			{
				return;
			}

			if (choiceIndex > -1)
			{
				_gameManager.ChangeRole(Player, _reservedRoles.Roles[choiceIndex], _reservedRoles.Behaviors[choiceIndex]);
			}
			else if (_reservedOnlyWerewolfs)
			{
				ChangeForRandomRole();
			}

			_gameManager.RemoveReservedRoles(this, new int[0]);
			_gameManager.RemovePlayerFromNightCall(NightPriorities[0], Player);
			_gameManager.StopWaintingForPlayer(Player);

			if (choiceIndex > -1 || _reservedOnlyWerewolfs)
			{
				Destroy(gameObject);
			}
		}

		public override void OnRoleTimeOut()
		{
			if (_reservedOnlyWerewolfs)
			{
				ChangeForRandomRole();
			}

			_gameManager.RemoveReservedRoles(this, new int[0]);
			_gameManager.RemovePlayerFromNightCall(NightPriorities[0], Player);

			if (_reservedOnlyWerewolfs)
			{
				Destroy(gameObject);
			}
		}

		private void ChangeForRandomRole()
		{
			int randomIndex = Random.Range(0, _reservedRoles.Roles.Length);
			_gameManager.ChangeRole(Player, _reservedRoles.Roles[randomIndex], _reservedRoles.Behaviors[randomIndex]);
		}
	}
}