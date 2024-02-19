using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public class WerewolfBehavior : RoleBehavior
	{
		[SerializeField]
		private float _voteMaxDuration;

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
			_preparedVote = _voteManager.PrepareVote(_voteMaxDuration, false);
			_voteManager.AddVoter(Player);
			_voteManager.AddVoteImmunity(Player);

			_voteManager.VoteCompletedCallback += OnVoteEnds;

			return true;
		}

		private void OnVoteEnds(Dictionary<PlayerRef, VoteManager.Vote> votes)
		{
			if (_preparedVote)
			{
				PlayerRef votedPlayer = PlayerRef.None;

				if (votes.Count <= 0)
				{
					return;
				}

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

				// TODO: Add a mark of death
				Debug.LogError($"Victim is {votedPlayer}");
			}

			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void OnRoleTimeOut() { }
	}
}