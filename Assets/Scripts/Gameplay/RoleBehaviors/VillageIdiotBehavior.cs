using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;
using static Werewolf.Managers.GameManager;

namespace Werewolf.Gameplay.Role
{
	public class VillageIdiotBehavior : RoleBehavior, IVoteManagerSubscriber
	{
		[Header("Execution")]
		[SerializeField]
		private MarkForDeathData _executionMarkForDeath;

		[SerializeField]
		private TitleScreenData _executionTitleScreen;

		[SerializeField]
		private GameHistoryEntryData _executedGameHistoryEntry;

		[SerializeField]
		private TitleScreenData _roleRevealTitleScreen;

		private bool _survivedExecution;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;
		private VoteManager _voteManager;

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;
			_voteManager = VoteManager.Instance;

			_gameManager.RevealDeadPlayerRoleStarted += OnRevealDeadPlayerRoleStarted;
			_gameManager.WaitBeforeFlipDeadPlayerRoleEnded += OnWaitBeforeFlipDeadPlayerRoleEnded;
			_voteManager.Subscribe(this);
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			return isWakingUp = false;
		}

		private void OnRevealDeadPlayerRoleStarted(PlayerRef playerRevealed)
		{
			if (Player == playerRevealed && !_survivedExecution && _gameManager.HasPlayerMarkForDeath(Player, _executionMarkForDeath) && _networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_DisplayTitle(Player, _executionTitleScreen.ID.HashCode);
			}
		}

		private void OnWaitBeforeFlipDeadPlayerRoleEnded(PlayerRef playerRevealed)
		{
			if (Player != playerRevealed || _survivedExecution || !_gameManager.HasPlayerMarkForDeath(Player, _executionMarkForDeath))
			{
				return;
			}

			_gameHistoryManager.AddEntry(_executedGameHistoryEntry.ID,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "Player",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (playerInfo.Key != Player && _networkDataManager.PlayerInfos[playerInfo.Key].IsConnected)
				{
					_gameManager.RPC_DisplayTitle(playerInfo.Key, _roleRevealTitleScreen.ID.HashCode);
				}
			}

			_gameManager.RemoveAllMarksForDeath(Player);

			_survivedExecution = true;
		}

		void IVoteManagerSubscriber.OnVoteStarting(ChoicePurpose purpose)
		{
			if (Player == null ||
				!_survivedExecution ||
				_gameManager.CurrentGameplayLoopStep != GameplayLoopStep.Execution ||
				!_gameManager.PlayerGameInfos[Player].IsAlive)
			{
				return;
			}

			_voteManager.RemoveVoter(Player);
			_voteManager.AddSpectator(Player);
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.RevealDeadPlayerRoleStarted -= OnRevealDeadPlayerRoleStarted;
			_gameManager.WaitBeforeFlipDeadPlayerRoleEnded -= OnWaitBeforeFlipDeadPlayerRoleEnded;
			_voteManager.Unsubscribe(this);
		}
	}
}
