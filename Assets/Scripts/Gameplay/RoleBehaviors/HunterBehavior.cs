using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public class HunterBehavior : RoleBehavior
	{
		[SerializeField]
		private float _selectedPlayerHighlightDuration = 3.0f;

		private GameManager _gameManager;

		public override void Init()
		{
			_gameManager = GameManager.Instance;
			_gameManager.PlayerDeathRevealEnded += OnPlayerDeathRevealEnded;
		}

		public override bool OnRoleCall()
		{
			return false;
		}

		private void OnPlayerDeathRevealEnded(PlayerRef deadPlayer)
		{
			if (Player != deadPlayer || !_gameManager.AskClientToChoosePlayer(Player, new[] { Player }, "Choose a player to kill", OnPlayerSelected))
			{
				return;
			}

			_gameManager.WaitForPlayer(Player);
			_gameManager.DisplayPlayerRoleIsPlaying(Player);
		}

		private void OnPlayerSelected(PlayerRef selectedPlayer)
		{
			if (selectedPlayer != PlayerRef.None)
			{
				_gameManager.AddMarkForDeath(selectedPlayer, "Shot", 1);
				_gameManager.RPC_SetPlayerCardHighlightVisible(selectedPlayer, true);
				_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
				_gameManager.SetPlayerCardHighlightVisible(selectedPlayer, true);
				_gameManager.HideUI();
#endif
				StartCoroutine(WaitBeforeRemovingHighlight(selectedPlayer));
				return;
			}

			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator WaitBeforeRemovingHighlight(PlayerRef selectedPlayer)
		{
			yield return new WaitForSeconds(_selectedPlayerHighlightDuration);

			_gameManager.RPC_SetPlayerCardHighlightVisible(selectedPlayer, false);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(selectedPlayer, false);
#endif
			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void OnRoleTimeOut() { }

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }
	}
}