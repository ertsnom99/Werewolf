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
		[Header("Data")]
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

		public override bool OnRoleCall(int nightCount, int priorityIndex)
		{
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

			if (!_preparedVote || firstPlayerVotedFor.IsNone || votes[firstPlayerVotedFor] < _voteManager.Voters.Count)
			{
				return;
			}

			_gameManager.AddMarkForDeath(votes.Keys.ToArray()[0], _commonData.MarkForDeath);
		}

		private void OnStartWaitingForPlayersRollCall()
		{
			_gameManager.StartWaitingForPlayersRollCall -= OnStartWaitingForPlayersRollCall;
			_voteManager.StartVote();
		}

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.StartWaitingForPlayersRollCall -= OnStartWaitingForPlayersRollCall;
		}
	}
}