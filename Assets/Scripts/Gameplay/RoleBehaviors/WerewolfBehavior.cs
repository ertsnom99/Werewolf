using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public class WerewolfBehavior : RoleBehavior
	{
		[SerializeField]
		private CommonWerewolfsData _commonData;

		private bool _preparedVote;

		private GameManager _gameManager;
		private VoteManager _voteManager;

		public override void Init()
		{
			_gameManager = GameManager.Instance;
			_voteManager = VoteManager.Instance;
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

		public override bool OnRoleCall()
		{
			_preparedVote = _voteManager.PrepareVote(_commonData.VoteMaxDuration, false);
			_voteManager.AddVoter(Player);
			_voteManager.AddVoteImmunity(Player);

			_voteManager.VoteCompletedCallback += OnVoteEnds;

			return true;
		}

		private void OnVoteEnds(Dictionary<PlayerRef, VoteManager.Vote> votes)
		{
			_voteManager.VoteCompletedCallback -= OnVoteEnds;

			_gameManager.StopWaintingForPlayer(Player);

			if (!_preparedVote || votes.Count <= 0)
			{
				return;
			}

			PlayerRef votedPlayer = PlayerRef.None;

			foreach (KeyValuePair<PlayerRef, VoteManager.Vote> vote in votes)
			{
				if (vote.Value.VotedFor == PlayerRef.None)
				{
					return;
				}

				if (votedPlayer == PlayerRef.None)
				{
					votedPlayer = vote.Value.VotedFor;
					continue;
				}

				if (votedPlayer != vote.Value.VotedFor)
				{
					return;
				}
			}

			_gameManager.AddMarkForDeath(votedPlayer, _commonData.DeathMark);
		}

		public override void OnRoleTimeOut() { }
	}
}