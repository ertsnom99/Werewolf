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

		private IEnumerator _endRoleCallAfterTimeCoroutine;

		private GameManager _gameManager;

		public override void Init()
		{
			_gameManager = GameManager.Instance;
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex)
		{
			List<PlayerRef> immunePlayers = _gameManager.GetPlayersDead();
			immunePlayers.Add(Player);

			if (!_gameManager.AskClientToChoosePlayers(Player,
													immunePlayers,
													"Choose a player to see his role",
													_choosePlayerMaximumDuration,
													false,
													1,
													ChoicePurpose.Other,
													OnPlayerSelected))
			{
				StartCoroutine(WaitToStopWaitingForPlayer());
			}

			_endRoleCallAfterTimeCoroutine = EndRoleCallAfterTime();
			StartCoroutine(_endRoleCallAfterTimeCoroutine);

			return true;
		}

		private IEnumerator WaitToStopWaitingForPlayer()
		{
			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnPlayerSelected(PlayerRef[] player)
		{
			StopCoroutine(_endRoleCallAfterTimeCoroutine);

			if (player.Length <= 0 || player[0].IsNone || !_gameManager.RevealPlayerRole(player[0], Player, false, true, OnRoleRevealed))
			{
				_gameManager.StopWaintingForPlayer(Player);
			}
		}

		private void OnRoleRevealed(PlayerRef revealTo)
		{
			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator EndRoleCallAfterTime()
		{
			float timeLeft = _choosePlayerMaximumDuration;

			while (timeLeft > 0)
			{
				yield return 0;
				timeLeft -= Time.deltaTime;
			}

			_gameManager.StopChoosingPlayers(Player);
			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}
	}
}