using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using Utilities.GameplayData;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class ComedianBehavior : RoleBehavior
	{
		[Header("Game Setup")]
		[SerializeField]
		private LocalizedString _notEnoughRolesWarning;

		[Header("Reserve Roles")]
		[SerializeField]
		private RoleData[] _prohibitedRoles;

		[SerializeField]
		private GameHistoryEntryData _wasGivenRolesGameHistoryEntry;

		[Header("Use Role")]
		[SerializeField]
		private ChoiceScreenData _choiceScreen;

		[SerializeField]
		private float _chooseReservedRoleMaximumDuration;

		[SerializeField]
		private GameHistoryEntryData _usedRoleGameHistoryEntry;

		private LocalizedStringListVariable _warningRoles;
		private GameManager.IndexedReservedRoles _reservedRoles;
		private RoleBehavior _currentRoleBehavior;
		private IEnumerator _endRoleCallAfterTimeCoroutine;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		private readonly int NEEDED_ROLE_COUNT = 3;

		public override bool IsRolesSetupValid(NetworkArray<NetworkRoleSetup> mandatoryRoles, NetworkArray<NetworkRoleSetup> optionalRoles, GameplayDataManager gameplayDataManager, List<LocalizedString> warnings)
		{
			List<RoleData> possibleRoles = new();
			int totalPossibleRolesCount = 0;

			if (CheckRoles(mandatoryRoles, possibleRoles, ref totalPossibleRolesCount) || CheckRoles(optionalRoles, possibleRoles, ref totalPossibleRolesCount))
			{
				return true;
			}
			else
			{
				if (_warningRoles == null)
				{
					_warningRoles = (LocalizedStringListVariable)_notEnoughRolesWarning["RoleNames"];
					_warningRoles.Values.Clear();

					foreach (RoleData prohibitedRole in _prohibitedRoles)
					{
						_warningRoles.Values.Add(prohibitedRole.CanHaveVariableAmount || prohibitedRole.MandatoryAmount > 1 ? prohibitedRole.NamePlural : prohibitedRole.NameSingular);
					}
				}

				warnings.Add(_notEnoughRolesWarning);
				return false;
			}

			bool CheckRoles(NetworkArray<NetworkRoleSetup> roles, List<RoleData> possibleRoles, ref int totalPossibleRolesCount)
			{
				for (int i = 0; i < roles.Length; i++)
				{
					int PoolCount = 0;
					int possibleRolesCount = 0;

					for (int j = 0; j < roles[i].Pool.Count(); j++)
					{
						if (roles[i].Pool[j] == 0)
						{
							break;
						}

						PoolCount++;

						if (gameplayDataManager.TryGetGameplayData(roles[i].Pool[j], out RoleData roleData) && IsRoleValid(roleData, possibleRoles))
						{
							possibleRoles.Add(roleData);
							possibleRolesCount++;
						}
					}

					if (PoolCount - possibleRolesCount < roles[i].UseCount)
					{
						totalPossibleRolesCount += roles[i].UseCount - PoolCount + possibleRolesCount;

						if (totalPossibleRolesCount >= NEEDED_ROLE_COUNT)
						{
							return true;
						}
					}
				}

				return false;
			}
		}

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_gameManager.RollCallBegin += OnRollCallBegin;
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute)
		{
			List<RoleSetup> mandatoryRolesCopy = new(mandatoryRoles);
			List<RoleSetup> availableRolesCopy = new(availableRoles);
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
				Debug.LogError($"{nameof(ComedianBehavior)} couldn't find enough roles to set aside!!!");
			}

			if (selectedRoles.Count <= 0)
			{
				return;
			}

			_gameManager.PrepareRoleBehaviors(rolesRequiringBehaviorPreparation.ToArray(), rolesToDistribute, mandatoryRoles, availableRoles);
			_gameManager.ReserveRoles(this, selectedRoles.ToArray(), true, false);

			_gameHistoryManager.AddEntry(_wasGivenRolesGameHistoryEntry.ID,
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

		private void SelectRolesFromRoleSetupList(List<RoleSetup> originalList, List<RoleSetup> listCopy, List<RoleData> selectedRoles, List<RoleData> rolesNeededToPrepare)
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

		private bool CanTakeRolesFromSetup(RoleSetup roleSetup, List<RoleData> selectedRoles)
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

		private List<RoleData> SelectRolesFromRoleSetup(RoleSetup roleSetup, List<RoleData> currentSelectedRoles)
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

			_gameManager.RemoveBehavior(Player, _currentRoleBehavior, removePlayerFromGroup: false);
			_currentRoleBehavior = null;
		}

		public override bool OnRoleCall(int priorityIndex, out bool isWakingUp)
		{
			_reservedRoles = _gameManager.GetReservedRoles(this);

			if (_reservedRoles.Roles == null)
			{
				return isWakingUp = false;
			}

			if (!_gameManager.ChooseReservedRole(this,
												_choiceScreen.ID.HashCode,
												false,
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

			_gameManager.AddBehavior(Player, _currentRoleBehavior, addPlayerToPlayerGroup: false);
			_gameManager.RemoveReservedRoles(this, new int[1] { selectedReservedRoleIndex });

			_gameHistoryManager.AddEntry(_usedRoleGameHistoryEntry.ID,
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
												Data = _currentRoleBehavior.RoleID.HashCode.ToString(),
												Type = GameHistorySaveEntryVariableType.RoleName
											}
										});

			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void OnPlayerChanged() { }

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