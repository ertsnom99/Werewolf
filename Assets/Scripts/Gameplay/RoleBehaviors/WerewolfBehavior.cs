using Assets.Scripts.Data.Tags;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;
using static Werewolf.GameHistoryManager;

namespace Werewolf
{
	public class WerewolfBehavior : RoleBehavior
	{
		[Header("Data")]
		[SerializeField]
		private CommonWerewolfsData _commonData;

		[Header("Vote")]
		[SerializeField]
		private GameplayTag _votedPlayerGameHistoryEntry;

		[SerializeField]
		private GameplayTag _failedToVotePlayerGameHistoryEntry;

		private bool _preparedVote;

		private GameManager _gameManager;
		private VoteManager _voteManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_voteManager = VoteManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;
		}

		public override void OnSelectedToDistribute(List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			isWakingUp = true;

			_preparedVote = _voteManager.PrepareVote(_commonData.VoteMaxDuration, true, false, ChoicePurpose.Kill);

			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_voteManager.AddVoter(Player);
			}

			_voteManager.AddVoteImmunity(Player);

			_voteManager.VoteCompleted += OnVoteEnded;

			if (_preparedVote)
			{
				_gameManager.StartWaitingForPlayersRollCall += OnStartWaitingForPlayersRollCall;
			}

			return true;
		}

		private void OnVoteEnded(Dictionary<PlayerRef, int> votes)
		{
			_voteManager.VoteCompleted -= OnVoteEnded;

			_gameManager.StopWaintingForPlayer(Player);

			PlayerRef firstPlayerVotedFor = votes.Count == 1 ? votes.Keys.ToArray()[0] : PlayerRef.None;

			if (!_preparedVote)
			{
				return;
			}

			if (!firstPlayerVotedFor.IsNone && votes[firstPlayerVotedFor] == _voteManager.Voters.Count)
			{
				_gameHistoryManager.AddEntry(_votedPlayerGameHistoryEntry,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "Player",
													Data = _networkDataManager.PlayerInfos[firstPlayerVotedFor].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												}
											});

				_gameManager.AddMarkForDeath(votes.Keys.ToArray()[0], _commonData.MarkForDeath);
			}
			else
			{
				_gameHistoryManager.AddEntry(_failedToVotePlayerGameHistoryEntry, null);
			}
		}

		private void OnStartWaitingForPlayersRollCall()
		{
			_gameManager.StartWaitingForPlayersRollCall -= OnStartWaitingForPlayersRollCall;
			_voteManager.StartVote();
		}

		public override void ReInitialize() { }

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.StartWaitingForPlayersRollCall -= OnStartWaitingForPlayersRollCall;
		}
	}
}