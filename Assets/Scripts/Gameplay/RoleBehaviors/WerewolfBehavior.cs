using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;

namespace Werewolf
{
	public class WerewolfBehavior : RoleBehavior
	{
		[SerializeField]
		private CommonWerewolfsData _commonData;

		private bool _preparedVote;

		private NetworkDataManager _networkDataManager;
		private GameManager _gameManager;
		private VoteManager _voteManager;

		public override void Init()
		{
			_networkDataManager = NetworkDataManager.Instance;
			_gameManager = GameManager.Instance;
			_voteManager = VoteManager.Instance;
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

		public override bool OnRoleCall()
		{
			_preparedVote = _voteManager.PrepareVote(_commonData.VoteMaxDuration, true, false);

			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_voteManager.AddVoter(Player);
			}

			_voteManager.AddVoteImmunity(Player);

			_voteManager.VoteCompletedCallback += OnVoteEnded;

			if (_preparedVote)
			{
				_gameManager.StartWaitingForPlayersRollCall += OnStartWaitingForPlayersRollCall;
			}

			return true;
		}

		private void OnVoteEnded(Dictionary<PlayerRef, int> votes)
		{
			_voteManager.VoteCompletedCallback -= OnVoteEnded;

			_gameManager.StopWaintingForPlayer(Player);

			PlayerRef firstPlayerVotedFor = votes.Count == 1 ? votes.Keys.ToArray()[0] : PlayerRef.None;

			if (!_preparedVote || firstPlayerVotedFor == PlayerRef.None || votes[firstPlayerVotedFor] < _voteManager.Voters.Count)
			{
				return;
			}

			_gameManager.AddMarkForDeath(votes.Keys.ToArray()[0], _commonData.DeathMark);
		}

		private void OnStartWaitingForPlayersRollCall()
		{
			_gameManager.StartWaitingForPlayersRollCall -= OnStartWaitingForPlayersRollCall;
			_voteManager.StartVote();
		}

		public override void OnRoleTimeOut() { }
	}
}