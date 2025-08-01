using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;
using static Werewolf.Managers.GameManager;

namespace Werewolf.Gameplay.Role
{
	public class LittleGirlBehavior : RoleBehavior, IVoteManagerSubscriber
	{
		[Header("Peek")]
		[SerializeField]
		private QuickActionScreenData _peekQuickActionScreen;

		[SerializeField]
		private GameHistoryEntryData _peekedGameHistoryEntry;

		[SerializeField]
		private GameHistoryEntryData _caughtPeekingGameHistoryEntry;

		[SerializeField]
		private TitleScreenData _caughtPeekingTitleScreen;

		[SerializeField]
		private TitleScreenData _werewolvesCaughtPeekingTitleScreen;

		private GameManager _gameManager;
		private VoteManager _voteManager;
		private GameHistoryManager _gameHistoryManager;
		protected NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			base.Initialize();

			_gameManager = GameManager.Instance;
			_voteManager = VoteManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_voteManager.Subscribe(this);
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int priorityIndex, out bool isWakingUp)
		{
			return isWakingUp = false;
		}

		void IVoteManagerSubscriber.OnVoteStarting(ChoicePurpose purpose)
		{
			if (Player.IsNone || !CanUsePower || _gameManager.CurrentGameplayLoopStep != GameplayLoopStep.NightCall)
			{
				return;
			}

			_gameManager.DisplayQuickAction(Player, _peekQuickActionScreen.ID.HashCode, OnPeeked);
			_voteManager.VoteCompleted += OnVoteEnded;
		}

		private void OnPeeked()
		{
			_voteManager.VoteCompleted -= OnVoteEnded;

			_gameManager.HideQuickAction(Player);
			_gameManager.RPC_SetPlayersCardHighlightVisible(Player, _voteManager.Voters.ToArray(), true);

			int result = Random.Range(1, 3);

			if (result == 1)
			{
				_gameHistoryManager.AddEntry(_peekedGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "Player",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												}
											});
			}
			else
			{
				_gameHistoryManager.AddEntry(_caughtPeekingGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "Player",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												}
											});

				_gameManager.RPC_DisplayTitle(Player, _caughtPeekingTitleScreen.ID.HashCode);

				foreach (PlayerRef voter in _voteManager.Voters)
				{
					_gameManager.RPC_DisplayTitle(voter, _werewolvesCaughtPeekingTitleScreen.ID.HashCode);
				}

				_voteManager.EndVote(Player);
			}
		}

		private void OnVoteEnded(Dictionary<PlayerRef, int> votes)
		{
			_voteManager.VoteCompleted -= OnVoteEnded;
			_gameManager.HideQuickAction(Player);
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_voteManager.Unsubscribe(this);
			_voteManager.VoteCompleted -= OnVoteEnded;
		}
	}
}