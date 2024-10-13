using Assets.Scripts.Data.Tags;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;
using static Werewolf.GameHistoryManager;

namespace Werewolf
{
	public class ThiefBehavior : RoleBehavior
	{
		[Header("Reserve Roles")]
		[SerializeField]
		private RoleData[] _rolesToAdd;

		[SerializeField]
		private GameplayTag _wasGivenRolesGameHistoryEntry;

		[Header("Choose Role")]
		[SerializeField]
		private float _chooseReservedRoleMaximumDuration = 10.0f;

		[SerializeField]
		private string _chooseRoleText;

		[SerializeField]
		private string _chooseRoleObligatoryText;

		[SerializeField]
		private string _choosedRoleText;

		[SerializeField]
		private GameplayTag _tookRoleGameHistoryEntry;

		private GameManager.IndexedReservedRoles _reservedRoles;
		private bool _reservedOnlyWerewolfs;

		private IEnumerator _endRoleCallAfterTimeCoroutine;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_gameManager.PreRoleDistribution += OnPreRoleDistribution;
			_gameManager.PostRoleDistribution += OnPostRoleDistribution;
		}

		public override void OnSelectedToDistribute(List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles, List<RoleData> rolesToDistribute) { }

		private void OnPreRoleDistribution()
		{
			_gameManager.AddRolesToDistribute(_rolesToAdd);
		}

		private void OnPostRoleDistribution()
		{
			List<RoleData> selectedRoles = new();

			for (int i = 0; i < _rolesToAdd.Length; i++)
			{
				if (_gameManager.RolesToDistribute.Count <= 0)
				{
					Debug.LogError("The thief couldn't find enough roles to set aside!!!");
					return;
				}

				int randomIndex = Random.Range(0, _gameManager.RolesToDistribute.Count);
				selectedRoles.Add(_gameManager.RolesToDistribute[randomIndex]);

				_gameManager.RemoveRoleToDistribute(_gameManager.RolesToDistribute[randomIndex]);
			}

			_gameManager.ReserveRoles(this, selectedRoles.ToArray(), false, true);

			_gameHistoryManager.AddEntry(_wasGivenRolesGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "FirstRoleName",
												Data = selectedRoles[0].GameplayTag.name,
												Type = GameHistorySaveEntryVariableType.RoleName
											},
											new()
											{
												Name = "SecondRoleName",
												Data = selectedRoles[1].GameplayTag.name,
												Type = GameHistorySaveEntryVariableType.RoleName
											}
										});
		}

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			isWakingUp = true;

			_reservedRoles = _gameManager.GetReservedRoles(this);

			if (_reservedRoles.Roles == null || _reservedRoles.Roles.Length < 0)
			{
				isWakingUp = false;
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

			if (!_gameManager.AskClientToChooseReservedRole(this, _chooseReservedRoleMaximumDuration, _reservedOnlyWerewolfs ? _chooseRoleObligatoryText : _chooseRoleText, _choosedRoleText, _reservedOnlyWerewolfs, OnRoleSelected))
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

			_gameManager.StopChoosingReservedRole(Player);
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
				RoleData selectedRole = _reservedRoles.Roles[choiceIndex];

				AddTookRoleGameHistoryEntry(selectedRole);
				_gameManager.ChangeRole(Player, selectedRole, _reservedRoles.Behaviors[choiceIndex]);
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

		private void AddTookRoleGameHistoryEntry(RoleData role)
		{
			_gameHistoryManager.AddEntry(_tookRoleGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "Player",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "RoleName",
												Data = role.GameplayTag.name,
												Type = GameHistorySaveEntryVariableType.RoleName
											}
										});
		}

		public override void ReInitialize() { }

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
			RoleData selectedRole = _reservedRoles.Roles[randomIndex];

			AddTookRoleGameHistoryEntry(selectedRole);
			_gameManager.ChangeRole(Player, selectedRole, _reservedRoles.Behaviors[randomIndex]);
		}

		private void OnDestroy()
		{
			_gameManager.PreRoleDistribution -= OnPreRoleDistribution;
			_gameManager.PostRoleDistribution -= OnPostRoleDistribution;
		}
	}
}