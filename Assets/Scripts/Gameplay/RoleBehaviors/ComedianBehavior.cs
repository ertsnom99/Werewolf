using Assets.Scripts.Data.Tags;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;
using static Werewolf.GameHistoryManager;

namespace Werewolf
{
	public class ComedianBehavior : RoleBehavior
	{
		[Header("Reserve Roles")]
		[SerializeField]
		private RoleData[] _prohibitedRoles;

		[SerializeField]
		private GameplayTag _wasGivenRolesGameHistoryEntry;

		[Header("Use Role")]
		[SerializeField]
		private GameplayTag _usedRoleGameHistoryEntry;

		[SerializeField]
		private float _chooseReservedRoleMaximumDuration = 10.0f;

		private GameManager.IndexedReservedRoles _reservedRoles;
		private RoleBehavior _currentRoleBehavior;

		private IEnumerator _endRoleCallAfterTimeCoroutine;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		private readonly int NEEDED_ROLE_COUNT = 3;

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_gameManager.RollCallBegin += OnRollCallBegin;
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

			_gameManager.ReserveRoles(this, selectedRoles.ToArray(), true, false);

			_gameHistoryManager.AddEntry(_wasGivenRolesGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "RoleNames",
												Data = ConcatenateRolesName(selectedRoles),
												Type = GameHistorySaveEntryVariableType.RoleNames
											}
										});
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
					&& !_prohibitedRoles.Contains(role)
					&& role.PrimaryType == PrimaryRoleType.Villager
					&& role.SecondaryType == SecondaryRoleType.None
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

		private void OnRollCallBegin()
		{
			if (!_currentRoleBehavior)
			{
				return;
			}

			_gameManager.RemoveBehavior(Player, _currentRoleBehavior, false, false);
			_currentRoleBehavior = null;
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

			if (!_gameManager.AskClientToChooseReservedRole(this, _chooseReservedRoleMaximumDuration, false, OnRoleSelected))
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

			if (choiceIndex <= -1)
			{
				_gameManager.StopWaintingForPlayer(Player);
				return;
			}

			int counter = 0;
			int selectedReservedRoleIndex;

			for (selectedReservedRoleIndex = 0; selectedReservedRoleIndex < _reservedRoles.Behaviors.Length; selectedReservedRoleIndex++)
			{
				if (!_reservedRoles.Behaviors[selectedReservedRoleIndex])
				{
					continue;
				}

				if (counter == choiceIndex)
				{
					_currentRoleBehavior = _reservedRoles.Behaviors[selectedReservedRoleIndex];
					break;
				}

				counter++;
			}

			_gameManager.AddBehavior(Player, _currentRoleBehavior, false);
			_gameManager.RemoveReservedRoles(this, new int[1] { selectedReservedRoleIndex });

			_gameHistoryManager.AddEntry(_usedRoleGameHistoryEntry,
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
												Data = _currentRoleBehavior.RoleGameplayTag.name,
												Type = GameHistorySaveEntryVariableType.RoleName
											}
										});

			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void ReInitialize() { }

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();

			_gameManager.StopChoosingReservedRole(Player);
		}

		private void OnDestroy()
		{
			_gameManager.RollCallBegin -= OnRollCallBegin;
		}
	}
}