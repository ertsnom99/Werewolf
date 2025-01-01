using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Assets.Scripts.Data.Tags;
using System.Collections;
using static Werewolf.Managers.GameHistoryManager;
using Werewolf.Network;

namespace Werewolf.Gameplay.Role
{
	public class BearTrainerBehavior : RoleBehavior
	{
		[Header("Find Werewolfs")]
		[SerializeField]
		private GameplayTag[] _werewolfPlayerGroups;

		[SerializeField]
		private GameplayTag _bearGrowlGameHistoryEntry;

		[SerializeField]
		private GameplayTag _bearGrowlImage;

		[SerializeField]
		private float _bearGrowlImageDuration = 5.0f;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
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
			if (Player == PlayerRef.None
			|| _gameManager.CurrentGameplayLoopStep != GameManager.GameplayLoopStep.DayDeathReveal
			|| !_gameManager.PlayerGameInfos[Player].IsAlive)
			{
				return;
			}

			StartCoroutine(CheckForWerewolfs());
		}

		private IEnumerator CheckForWerewolfs()
		{
			List<PlayerRef> playersToCheck = _gameManager.FindSurroundingPlayers(Player);
			playersToCheck.Add(Player);

			if (!_gameManager.IsAnyPlayersInPlayerGroups(playersToCheck, _werewolfPlayerGroups))
			{
				yield break;
			}

			_gameManager.WaitForPlayer(Player);

			_gameHistoryManager.AddEntry(_bearGrowlGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "BearTrainerPlayer",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			_gameManager.RPC_DisplayTitle(_bearGrowlImage.CompactTagId);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.DisplayTitle(_bearGrowlImage.CompactTagId);
#endif
			yield return new WaitForSeconds(_gameManager.Config.UITransitionNormalDuration + _bearGrowlImageDuration * _gameManager.GameSpeedModifier);
			
			_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(_gameManager.Config.UITransitionNormalDuration);

			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void ReInitialize() { }

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.DeathRevealEnded -= OnDeathRevealEnded;
		}
	}
}
