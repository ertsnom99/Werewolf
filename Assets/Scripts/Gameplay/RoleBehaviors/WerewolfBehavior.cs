using Fusion;
using System.Collections.Generic;
using System.Linq;
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

			_voteManager.VoteCompletedCallback += OnVoteEnded;

			return true;
		}

		private void OnVoteEnded(Dictionary<PlayerRef, int> votes)
		{
			_voteManager.VoteCompletedCallback -= OnVoteEnded;

			_gameManager.StopWaintingForPlayer(Player);

			if (!_preparedVote || votes.Count != 1)
			{
				return;
			}

			_gameManager.AddMarkForDeath(votes.Keys.ToArray()[0], _commonData.DeathMark);
		}

		public override void OnRoleTimeOut() { }
	}
}