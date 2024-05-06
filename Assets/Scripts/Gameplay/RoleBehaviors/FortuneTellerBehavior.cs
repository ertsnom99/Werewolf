using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public class FortuneTellerBehavior : RoleBehavior
	{
		[SerializeField]
		private float _choosePlayerMaximumDuration = 10.0f;

		private GameManager _gameManager;

		public override void Init()
		{
			_gameManager = GameManager.Instance;
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

		public override bool OnRoleCall()
		{
			List<PlayerRef> immunePlayers = new() { Player };

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (playerInfo.Value.IsAlive)
				{
					continue;
				}

				immunePlayers.Add(playerInfo.Key);
			}

			if (!_gameManager.AskClientToChoosePlayer(Player,
													immunePlayers.ToArray(),
													"Choose a player to see his role",
													_choosePlayerMaximumDuration,
													false,
													OnPlayerSelected))
			{
				StartCoroutine(WaitToStopWaitingForPlayer());
			}

			return true;
		}

		private IEnumerator WaitToStopWaitingForPlayer()
		{
			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnPlayerSelected(PlayerRef player)
		{
			if (player.IsNone || !_gameManager.RevealPlayerRole(player, Player, false, true, OnRoleRevealed))
			{
				_gameManager.StopWaintingForPlayer(Player);
			}
		}

		private void OnRoleRevealed(PlayerRef revealTo)
		{
			_gameManager.StopWaintingForPlayer(Player);
		}
	}
}