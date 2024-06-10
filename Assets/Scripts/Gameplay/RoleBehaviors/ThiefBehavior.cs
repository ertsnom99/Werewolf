using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public class ThiefBehavior : RoleBehavior
	{
		[Header("Choose Role")]
		[SerializeField]
		private RoleData[] _rolesToAdd;

		[SerializeField]
		private float _chooseReservedRoleMaximumDuration = 10.0f;

		private GameManager.IndexedReservedRoles _reservedRoles;
		private bool _reservedOnlyWerewolfs;

		private IEnumerator _endRoleCallAfterTimeCoroutine;

		private GameManager _gameManager;

		public override void Init()
		{
			_gameManager = GameManager.Instance;

			_gameManager.PreRoleDistribution += OnPreRoleDistribution;
			_gameManager.PostRoleDistribution += OnPostRoleDistribution;
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

			_gameManager.ReserveRoles(this, roles.ToArray(), false, true);
		}

		public override bool OnRoleCall(int nightCount, int priorityIndex)
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

			if (!_gameManager.AskClientToChooseReservedRole(this, _chooseReservedRoleMaximumDuration, _reservedOnlyWerewolfs, OnRoleSelected))
			{
				StartCoroutine(WaitOnRoleSelected(-1));
			}

			_endRoleCallAfterTimeCoroutine = EndRoleCallAfterTime();
			StartCoroutine(_endRoleCallAfterTimeCoroutine);

			return true;
		}

		private IEnumerator WaitOnRoleSelected(int choiceIndex)
		{
			yield return 0;
			OnRoleSelected(choiceIndex);
		}

		private IEnumerator EndRoleCallAfterTime()
		{
			float timeLeft = _chooseReservedRoleMaximumDuration;

			while (timeLeft > 0)
			{
				yield return 0;
				timeLeft -= Time.deltaTime;
			}

			OnRoleSelected(-1);
		}

		private void OnRoleSelected(int choiceIndex)
		{
			if (_endRoleCallAfterTimeCoroutine != null)
			{
				StopCoroutine(_endRoleCallAfterTimeCoroutine);
				_endRoleCallAfterTimeCoroutine = null;
			}

			PlayerRef previousPlayer = Player;

			if (choiceIndex > -1)
			{
				_gameManager.ChangeRole(Player, _reservedRoles.Roles[choiceIndex], _reservedRoles.Behaviors[choiceIndex]);
			}
			else if (_reservedOnlyWerewolfs)
			{
				ChangeForRandomRole();
			}

			_gameManager.RemoveReservedRoles(this, new int[0]);
			_gameManager.StopWaintingForPlayer(previousPlayer);

			if (choiceIndex > -1 || _reservedOnlyWerewolfs)
			{
				Destroy(gameObject);
			}
		}

		public override void ReInit() { }

		public override void OnRoleCallDisconnected()
		{
			if (_endRoleCallAfterTimeCoroutine == null)
			{
				return;
			}

			StopCoroutine(_endRoleCallAfterTimeCoroutine);
			_endRoleCallAfterTimeCoroutine = null;
			
			_gameManager.StopChoosingReservedRole(Player);

			if (_reservedOnlyWerewolfs)
			{
				ChangeForRandomRole();
			}

			_gameManager.RemoveReservedRoles(this, new int[0]);

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

		private void OnDestroy()
		{
			_gameManager.PreRoleDistribution -= OnPreRoleDistribution;
			_gameManager.PostRoleDistribution -= OnPostRoleDistribution;
		}
	}
}