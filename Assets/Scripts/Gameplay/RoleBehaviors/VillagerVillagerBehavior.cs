using System.Collections.Generic;
using Fusion;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;

namespace Werewolf.Gameplay.Role
{
	public class VillagerVillagerBehavior : RoleBehavior
	{
		private PlayerRef _currentPlayer;

		private GameManager _gameManager;

		public override void Initialize()
		{
			base.Initialize();

			_gameManager = GameManager.Instance;

			_gameManager.StartedPlayersInitialization += OnStartedPlayersInitialization;
			_gameManager.WaitingForPlayersRollCallEnded += OnWaitingForPlayersRollCallEnded;
			_gameManager.RevealDeadPlayerRoleEnded += OnRevealDeadPlayerRoleEnded;
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int priorityIndex, out bool isWakingUp)
		{
			return isWakingUp = false;
		}

		public override void OnAddedReservedRoleID(int[] roleIDs, int index)
		{
			roleIDs[index] = RoleID.HashCode;
		}

		private void OnStartedPlayersInitialization()
		{
			UpdateCurrentPlayer();
			_gameManager.StartedPlayersInitialization -= OnStartedPlayersInitialization;
		}

		private void OnWaitingForPlayersRollCallEnded()
		{
			if (_currentPlayer != Player)
			{
				UpdateCurrentPlayer();
			}
		}

		private void OnRevealDeadPlayerRoleEnded(PlayerRef deadPlayer, MarkForDeathData markForDeath)
		{
			if (_currentPlayer != Player)
			{
				UpdateCurrentPlayer();
			}
		}

		private void UpdateCurrentPlayer()
		{
			_currentPlayer = Player;

			if (!Player.IsNone)
			{
				_gameManager.RevealPlayerRole(Player, RoleID.HashCode);
			}
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.StartedPlayersInitialization -= OnStartedPlayersInitialization;
			_gameManager.WaitingForPlayersRollCallEnded -= OnWaitingForPlayersRollCallEnded;
			_gameManager.RevealDeadPlayerRoleEnded -= OnRevealDeadPlayerRoleEnded;
		}
	}
}
