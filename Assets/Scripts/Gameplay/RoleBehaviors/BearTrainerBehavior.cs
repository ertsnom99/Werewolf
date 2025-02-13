using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using System.Collections;
using static Werewolf.Managers.GameHistoryManager;
using Werewolf.Network;
using Utilities.GameplayData;

namespace Werewolf.Gameplay.Role
{
	public class BearTrainerBehavior : RoleBehavior
	{
		[Header("Find Werewolfs")]
		[SerializeField]
		private PlayerGroupData[] _werewolvesPlayerGroups;

		[SerializeField]
		private GameHistoryEntryData _bearGrowlGameHistoryEntry;

		[SerializeField]
		private ImageData _bearGrowlTitle;

		[SerializeField]
		private float _bearGrowlTitleDuration;

		private UniqueID[] _werewolvesPlayerGroupIDs;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			_werewolvesPlayerGroupIDs = GameplayData.GetIDs(_werewolvesPlayerGroups);

			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_gameManager.DeathRevealEnded += OnDeathRevealEnded;
		}

		public override void OnSelectedToDistribute(List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			return isWakingUp = false;
		}

		private void OnDeathRevealEnded()
		{
			if (Player.IsNone
			|| _gameManager.CurrentGameplayLoopStep != GameManager.GameplayLoopStep.DayDeathReveal
			|| !_gameManager.PlayerGameInfos[Player].IsAlive)
			{
				return;
			}

			StartCoroutine(CheckForWerewolfs());
		}

		private IEnumerator CheckForWerewolfs()
		{
			HashSet<PlayerRef> playersToCheck = _gameManager.FindSurroundingPlayers(Player);
			playersToCheck.Add(Player);

			if (!_gameManager.IsAnyPlayersInPlayerGroups(playersToCheck, _werewolvesPlayerGroupIDs))
			{
				yield break;
			}

			_gameManager.WaitForPlayer(Player);

			_gameHistoryManager.AddEntry(_bearGrowlGameHistoryEntry.ID,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "BearTrainerPlayer",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			_gameManager.RPC_DisplayTitle(_bearGrowlTitle.ID.HashCode);

			GameConfig gameConfig = _gameManager.Config;
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.DisplayTitle(_bearGrowlTitle.ID.HashCode);
#endif
			yield return new WaitForSeconds(gameConfig.UITransitionNormalDuration + _bearGrowlTitleDuration * _gameManager.GameSpeedModifier);
			
			_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(gameConfig.UITransitionNormalDuration);

			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.DeathRevealEnded -= OnDeathRevealEnded;
		}
	}
}
