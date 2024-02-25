using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public class FortuneTellerBehavior : RoleBehavior
	{
		private GameManager _gameManager;

		public override void Init()
		{
			_gameManager = GameManager.Instance;
		}

		public override bool OnRoleCall()
		{
			return _gameManager.AskClientToChoosePlayer(Player, new PlayerRef[] { Player }, "Choose a player to see his role", OnPlayerSelected);
		}

		private void OnPlayerSelected(PlayerRef player)
		{
			if (_timedOut)
			{
				return;
			}

			if (player == PlayerRef.None)
			{
				_gameManager.StopWaintingForPlayer(Player);
				return;
			}

			// TODO: Reveal the player role to Player
		}

		public override void OnRoleTimeOut()
		{
			// TODO: Cancel selection
			// TODO: What if is revealing role when time out happens?
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }
	}
}