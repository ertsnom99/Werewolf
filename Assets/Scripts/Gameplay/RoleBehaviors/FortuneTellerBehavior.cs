using Fusion;
using System.Collections.Generic;
using Werewolf.Data;

namespace Werewolf
{
	public class FortuneTellerBehavior : RoleBehavior
	{
		private bool _choosingPlayer;

		private GameManager _gameManager;

		public override void Init()
		{
			_gameManager = GameManager.Instance;
		}

		public override bool OnRoleCall()
		{
			_choosingPlayer = _gameManager.AskClientToChoosePlayer(Player, new PlayerRef[] { Player }, "Choose a player to see his role", OnPlayerSelected);
			return _choosingPlayer;
		}

		private void OnPlayerSelected(PlayerRef player)
		{
			if (_timedOut)
			{
				return;
			}

			_choosingPlayer = false;

			if (player == PlayerRef.None)
			{
				_gameManager.StopWaintingForPlayer(Player);
				return;
			}

			// TODO: Reveal the player role to Player
		}

		public override void OnRoleTimeOut()
		{
			if (!_choosingPlayer)
			{
				return;
			}

			_gameManager.RemoveChoosePlayerCallback(Player);
			_gameManager.RPC_ClientStopChoosingPlayer(Player);

			// TODO: What if is revealing role when time out happens?
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }
	}
}