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
			List<PlayerRef> immunePlayers = new() { Player };

			foreach (KeyValuePair<PlayerRef, GameManager.PlayerData> player in _gameManager.Players)
			{
				if (player.Value.IsAlive)
				{
					continue;
				}

				immunePlayers.Add(player.Key);
			}

			_choosingPlayer = _gameManager.AskClientToChoosePlayer(Player, immunePlayers.ToArray(), "Choose a player to see his role", OnPlayerSelected);
			return _choosingPlayer;
		}

		private void OnPlayerSelected(PlayerRef player)
		{
			if (_timedOut)
			{
				return;
			}

			_choosingPlayer = false;

			if (player == PlayerRef.None || !_gameManager.RevealPlayerRole(player, Player, false, true, OnRoleRevealed))
			{
				_gameManager.StopWaintingForPlayer(Player);
			}
		}

		private void OnRoleRevealed(PlayerRef revealTo)
		{
			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void OnRoleTimeOut()
		{
			if (!_choosingPlayer)
			{
				return;
			}

			_gameManager.StopChoosingPlayer(Player);
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }
	}
}