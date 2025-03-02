using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Utilities.GameplayData;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;
using static Werewolf.Managers.GameManager;

namespace Werewolf.Gameplay.Role
{
	public class ThiefBehavior : RoleBehavior
	{
		[Header("Game Setup")]
		[SerializeField]
		private LocalizedString _notEnoughWerewolvesWarning;

		[Header("Reserve Roles")]
		[SerializeField]
		private RoleData[] _rolesToAdd;

		[SerializeField]
		private GameHistoryEntryData _wasGivenRolesGameHistoryEntry;

		[Header("Choose Role")]
		[SerializeField]
		private float _chooseReservedRoleMaximumDuration;

		[SerializeField]
		private ChoiceScreenData _mayChooseChoiceScreen;

		[SerializeField]
		private ChoiceScreenData _mustChooseChoiceScreen;

		[SerializeField]
		private GameHistoryEntryData _tookRoleGameHistoryEntry;

		private bool _choseRole;

		private IndexedReservedRoles _reservedRoles;
		private bool _reservedOnlyWerewolves;

		private IEnumerator _endRoleCallAfterTimeCoroutine;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override bool IsRolesSetupValid(NetworkArray<NetworkRoleSetup> mandatoryRoles, NetworkArray<NetworkRoleSetup> optionalRoles, GameplayDataManager gameplayDataManager, List<LocalizedString> warnings)
		{
			int werewolvesCount = 0;

			for (int i = 0; i < mandatoryRoles.Length; i++)
			{
				for (int j = 0; j < mandatoryRoles[i].UseCount; j++)
				{
					if (gameplayDataManager.TryGetGameplayData(mandatoryRoles[i].Pool[j], out RoleData roleData) && roleData.PrimaryType == PrimaryRoleType.Werewolf)
					{
						if (werewolvesCount == 1)
						{
							return true;
						}
						else
						{
							werewolvesCount++;
						}
					}
				}
			}

			warnings.Add(_notEnoughWerewolvesWarning);
			return false;
		}

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_gameManager.PreRoleDistribution += OnPreRoleDistribution;
			_gameManager.PostRoleDistribution += OnPostRoleDistribution;
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

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
					Debug.LogError($"{nameof(ThiefBehavior)} couldn't find enough roles to set aside!!!");
					return;
				}

				int randomIndex = Random.Range(0, _gameManager.RolesToDistribute.Count);
				selectedRoles.Add(_gameManager.RolesToDistribute[randomIndex]);

				_gameManager.RemoveRoleToDistribute(_gameManager.RolesToDistribute[randomIndex]);
			}

			_gameManager.ReserveRoles(this, selectedRoles.ToArray(), false, true);

			_gameHistoryManager.AddEntry(_wasGivenRolesGameHistoryEntry.ID,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "FirstRoleName",
												Data = selectedRoles[0].ID.HashCode.ToString(),
												Type = GameHistorySaveEntryVariableType.RoleName
											},
											new()
											{
												Name = "SecondRoleName",
												Data = selectedRoles[1].ID.HashCode.ToString(),
												Type = GameHistorySaveEntryVariableType.RoleName
											}
										});
		}

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			if (_choseRole)
			{
				return isWakingUp = false;
			}

			_reservedRoles = _gameManager.GetReservedRoles(this);

			if (_reservedRoles.Roles == null)
			{
				return isWakingUp = false;
			}

			_reservedOnlyWerewolves = true;

			foreach (RoleData role in _reservedRoles.Roles)
			{
				if (role.PrimaryType != PrimaryRoleType.Werewolf)
				{
					_reservedOnlyWerewolves = false;
					break;
				}
			}

			if (!_gameManager.ChooseReservedRole(this,
												_reservedOnlyWerewolves ? _mustChooseChoiceScreen.ID.HashCode : _mayChooseChoiceScreen.ID.HashCode,
												_reservedOnlyWerewolves,
												_chooseReservedRoleMaximumDuration * _gameManager.GameSpeedModifier,
												OnRoleSelected))
			{
				StartCoroutine(WaitOnRoleSelected(-1));
			}

			_endRoleCallAfterTimeCoroutine = EndRoleCallAfterTime();
			StartCoroutine(_endRoleCallAfterTimeCoroutine);

			return isWakingUp = true;
		}

		private IEnumerator WaitOnRoleSelected(int choiceIndex)
		{
			yield return 0;
			OnRoleSelected(choiceIndex);
		}

		private IEnumerator EndRoleCallAfterTime()
		{
			float timeLeft = _chooseReservedRoleMaximumDuration * _gameManager.GameSpeedModifier;

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
			else if (_reservedOnlyWerewolves)
			{
				ChangeForRandomRole();
			}

			_gameManager.RemoveReservedRoles(this, new int[0]);
			_choseRole = true;

			_gameManager.StopWaintingForPlayer(previousPlayer);

			if (choiceIndex > -1 || _reservedOnlyWerewolves)
			{
				Destroy(gameObject);
			}
		}

		private void AddTookRoleGameHistoryEntry(RoleData role)
		{
			_gameHistoryManager.AddEntry(_tookRoleGameHistoryEntry.ID,
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
												Data = role.ID.HashCode.ToString(),
												Type = GameHistorySaveEntryVariableType.RoleName
											}
										});
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected()
		{
			if (_endRoleCallAfterTimeCoroutine == null)
			{
				return;
			}

			StopCoroutine(_endRoleCallAfterTimeCoroutine);
			_endRoleCallAfterTimeCoroutine = null;
			
			_gameManager.StopChoosingReservedRole(Player);

			if (_reservedOnlyWerewolves)
			{
				ChangeForRandomRole();
			}

			_gameManager.RemoveReservedRoles(this, new int[0]);

			if (_reservedOnlyWerewolves)
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