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

		[SerializeField]
		private string _chooseRoleText;

		[SerializeField]
		private string _choosedRoleText;

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

		public override void OnSelectedToDistribute(List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles, List<RoleData> rolesToDistribute)
		{
			List<RoleSetupData> mandatoryRolesCopy = new(mandatoryRoles);
			List<RoleSetupData> availableRolesCopy = new(availableRoles);
			List<RoleData> rolesToDistributeCopy = new(rolesToDistribute);

			List<RoleData> selectedRoles = new();
			List<RoleData> rolesRequiringBehaviorPreparation = new();

			while (selectedRoles.Count < NEEDED_ROLE_COUNT && (mandatoryRolesCopy.Count > 0 || availableRolesCopy.Count > 0 || rolesToDistributeCopy.Count > 0))
			{
				int roleListIndex = Random.Range(0, 3);
				bool choosingList = true;

				while(choosingList)
				{
					switch (roleListIndex)
					{
						case 0:
							if (rolesToDistributeCopy.Count > 0)
							{
								SelectRoleFromRoleDataList(rolesToDistribute, rolesToDistributeCopy, selectedRoles);
								choosingList = false;
							}
							break;
						case 1:
							if (mandatoryRolesCopy.Count > 0)
							{
								SelectRolesFromRoleSetupList(mandatoryRoles, mandatoryRolesCopy, selectedRoles, rolesRequiringBehaviorPreparation);
								choosingList = false;
							}
							break;
						case 2:
							if (availableRolesCopy.Count > 0)
							{
								SelectRolesFromRoleSetupList(availableRoles, availableRolesCopy, selectedRoles, rolesRequiringBehaviorPreparation);
								choosingList = false;
							}
							break;
					}

					roleListIndex = (roleListIndex + 1) % 3;
				}
			}

			if (selectedRoles.Count < NEEDED_ROLE_COUNT)
			{
				Debug.LogError("The comedian couldn't find enough roles to set aside!!!");
			}

			if (selectedRoles.Count <= 0)
			{
				return;
			}

			_gameManager.PrepareRoleBehaviors(rolesRequiringBehaviorPreparation.ToArray(), rolesToDistribute, mandatoryRoles, availableRoles);
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

		private void SelectRoleFromRoleDataList(List<RoleData> originalList, List<RoleData> listCopy, List<RoleData> selectedRoles)
		{
			int roleIndex = Random.Range(0, listCopy.Count);

			if (!IsRoleValid(listCopy[roleIndex], selectedRoles))
			{
				listCopy.RemoveAt(roleIndex);
			}
			else
			{
				selectedRoles.Add(listCopy[roleIndex]);

				originalList.Remove(listCopy[roleIndex]);
				listCopy.RemoveAt(roleIndex);
			}
		}

		private void SelectRolesFromRoleSetupList(List<RoleSetupData> originalList, List<RoleSetupData> listCopy, List<RoleData> selectedRoles, List<RoleData> rolesNeededToPrepare)
		{
			int roleSetupIndex = Random.Range(0, listCopy.Count);

			if (!CanTakeRolesFromSetup(listCopy[roleSetupIndex], selectedRoles))
			{
				listCopy.RemoveAt(roleSetupIndex);
			}
			else
			{
				List<RoleData> newSelectedRoles = SelectRolesFromRoleSetup(listCopy[roleSetupIndex], selectedRoles);
				selectedRoles.AddRange(newSelectedRoles);
				rolesNeededToPrepare.AddRange(newSelectedRoles);

				originalList.Remove(listCopy[roleSetupIndex]);
				listCopy.RemoveAt(roleSetupIndex);
			}
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

		private List<RoleData> SelectRolesFromRoleSetup(RoleSetupData roleSetup, List<RoleData> currentSelectedRoles)
		{
			List<RoleData> selectedRoles = new();
			int indexOffset = Random.Range(0, roleSetup.UseCount);

			for (int i = 0; i < roleSetup.Pool.Length; i++)
			{
				int adjustedIndex = (i + indexOffset) % roleSetup.Pool.Length;

				if (IsRoleValid(roleSetup.Pool[adjustedIndex], currentSelectedRoles))
				{
					selectedRoles.Add(roleSetup.Pool[adjustedIndex]);
				}

				if (selectedRoles.Count >= roleSetup.UseCount)
				{
					break;
				}
			}

			return selectedRoles;
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

			if (!_gameManager.AskClientToChooseReservedRole(this, _chooseReservedRoleMaximumDuration * _gameManager.GameSpeedModifier, _chooseRoleText, _choosedRoleText, false, OnRoleSelected))
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