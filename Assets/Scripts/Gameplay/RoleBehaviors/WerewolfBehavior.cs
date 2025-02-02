using Fusion;
using System.Collections;
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
			VoteForVillager();
			return isWakingUp = true;
		}

		protected void VoteForVillager()
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

		protected virtual void OnVoteEnded(Dictionary<PlayerRef, int> votes)
		{
			_voteManager.VoteCompleted -= OnVoteEnded;

			if (!_preparedVote)
			{
				_gameManager.StopWaintingForPlayer(Player);
				return;
			}

			StartCoroutine(EndRoleCall(votes));
		}

		private IEnumerator EndRoleCall(Dictionary<PlayerRef, int> votes)
		{
			SetWerewolfIconsVisible(false);

			PlayerRef firstPlayerVotedFor = votes.Count == 1 ? votes.Keys.ToArray()[0] : PlayerRef.None;

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

				foreach (PlayerRef voter in _voteManager.Voters)
				{
					if (_networkDataManager.PlayerInfos[voter].IsConnected)
					{
						_gameManager.RPC_SetPlayerCardHighlightVisible(voter, firstPlayerVotedFor, true);
					}
				}

				foreach (PlayerRef spectators in _voteManager.Spectators)
				{
					if (_networkDataManager.PlayerInfos[spectators].IsConnected)
					{
						_gameManager.RPC_SetPlayerCardHighlightVisible(spectators, firstPlayerVotedFor, true);
					}
				}
#if UNITY_SERVER && UNITY_EDITOR
				_gameManager.SetPlayerCardHighlightVisible(firstPlayerVotedFor, true);
#endif
				yield return new WaitForSeconds(_commonWerewolfsData.ChoosenVillagerHighlightDuration);
			}
			else
			{
				_gameHistoryManager.AddEntry(_commonWerewolfsData.FailedToVotePlayerGameHistoryEntry, null);
			}

			_gameManager.StopWaintingForPlayer(Player);
		}

		private void SetWerewolfIconsVisible(bool isVisible)
		{
			var voters = _voteManager.Voters.ToArray();

			foreach (PlayerRef voter in voters)
			{
				if (_networkDataManager.PlayerInfos[voter].IsConnected)
				{
					_gameManager.RPC_SetPlayersCardWerewolfIconVisible(voter, voters, isVisible);
				}
			}
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected() { }

		protected virtual void OnDestroy()
		{
			_gameManager.StartWaitingForPlayersRollCall -= OnStartWaitingForPlayersRollCall;
		}
	}
}