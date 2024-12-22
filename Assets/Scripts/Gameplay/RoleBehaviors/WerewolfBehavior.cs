using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class WerewolfBehavior : RoleBehavior
	{
		[Header("Data")]
		[SerializeField]
		protected CommonWerewolfsData _commonWerewolfsData;

		private bool _preparedVote;

		protected GameManager _gameManager;
		protected VoteManager _voteManager;
		protected GameHistoryManager _gameHistoryManager;
		protected NetworkDataManager _networkDataManager;

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
			VoteForVillagers();
			return isWakingUp = true;
		}

		protected void VoteForVillagers()
		{
			_preparedVote = _voteManager.PrepareVote(_commonWerewolfsData.VotePlayerImage.CompactTagId, _commonWerewolfsData.VoteMaxDuration * _gameManager.GameSpeedModifier, false, ChoicePurpose.Kill);

			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_voteManager.AddVoter(Player);
			}

			_voteManager.AddVoteImmunity(Player);

			if (_preparedVote)
			{
				foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerGameInfo in _gameManager.PlayerGameInfos)
				{
					if (playerGameInfo.Value.IsAlive)
					{
						continue;
					}

					_voteManager.AddVoteImmunity(playerGameInfo.Key);
				}

				_gameManager.StartWaitingForPlayersRollCall += OnStartWaitingForPlayersRollCall;
			}

			_voteManager.VoteCompleted += OnVoteEnded;
		}

		private void OnStartWaitingForPlayersRollCall()
		{
			_gameManager.StartWaitingForPlayersRollCall -= OnStartWaitingForPlayersRollCall;

			SetWerewolfIconsVisible(true);
			_voteManager.StartVote();
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

			SetWerewolfIconsVisible(false);

			if (!firstPlayerVotedFor.IsNone && votes[firstPlayerVotedFor] == _voteManager.Voters.Count)
			{
				_gameHistoryManager.AddEntry(_commonWerewolfsData.VotedPlayerGameHistoryEntry,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "Player",
													Data = _networkDataManager.PlayerInfos[firstPlayerVotedFor].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												}
											});

				_gameManager.AddMarkForDeath(firstPlayerVotedFor, _commonWerewolfsData.MarkForDeath);
			}
			else
			{
				_gameHistoryManager.AddEntry(_commonWerewolfsData.FailedToVotePlayerGameHistoryEntry, null);
			}
		}

		private void SetWerewolfIconsVisible(bool isVisible)
		{
			var voters = _voteManager.Voters.ToArray();

			foreach (PlayerRef voter in voters)
			{
				if (!_networkDataManager.PlayerInfos[voter].IsConnected)
				{
					continue;
				}

				_gameManager.RPC_SetPlayersCardWerewolfIconVisible(voter, voters, isVisible);
			}
		}

		public override void ReInitialize() { }

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.StartWaitingForPlayersRollCall -= OnStartWaitingForPlayersRollCall;
		}
	}
}