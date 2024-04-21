using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public class ServantBehavior : RoleBehavior
	{
		[SerializeField]
		private float _servantRevealDuration = 3.0f;

		private bool _isWaitingForPromptAnswer;
		private PlayerRef _playerRevealed;

		private GameManager _gameManager;

		public override void Init()
		{
			_gameManager = GameManager.Instance;

			_gameManager.WaitBeforeDeathRevealStarted += OnWaitBeforeDeathRevealStarted;
			_gameManager.WaitBeforeDeathRevealEnded += OnWaitBeforeDeathRevealEnded;
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

		public override bool OnRoleCall()
		{
			return false;
		}

		public override void OnRoleTimeOut() { }

		private void OnWaitBeforeDeathRevealStarted(PlayerRef playerRevealed, List<string> marks, float revealDuration)
		{
			if (Player == PlayerRef.None
				|| !_gameManager.PlayerGameInfos[Player].IsAlive
				|| Player == playerRevealed
				|| !marks.Contains(_gameManager.Config.ExecutionMarkForDeath)
				|| !_gameManager.PromptPlayer(Player, "Take this role?", revealDuration, "Take", OnTakeRole))
			{
				return;
			}

			_isWaitingForPromptAnswer = true;
			_playerRevealed = playerRevealed;
		}

		private void OnTakeRole(PlayerRef player)
		{
			_isWaitingForPromptAnswer = false;
			_gameManager.StopPlayerDeathReveal();

			StartCoroutine(ChangeRole());
		}

		private IEnumerator ChangeRole()
		{
			RoleData RoleToTake = _gameManager.PlayerGameInfos[_playerRevealed].Role;
			RoleData servantRole = _gameManager.PlayerGameInfos[Player].Role;

			_gameManager.TransferRole(_playerRevealed, Player, false);

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (playerInfo.Key == Player)
				{
					_gameManager.RPC_FlipCard(playerInfo.Key, _playerRevealed, RoleToTake.GameplayTag.CompactTagId);
					_gameManager.RPC_DisplayTitle(playerInfo.Key, $"Your new role is: {RoleToTake.Name}");
					continue;
				}

				if (playerInfo.Key == _playerRevealed)
				{
					_gameManager.RPC_HideUI(_playerRevealed);
				}

				_gameManager.RPC_MoveCardToCamera(playerInfo.Key, Player, true, servantRole.GameplayTag.CompactTagId);
				_gameManager.RPC_DestroyPlayerCard(playerInfo.Key, _playerRevealed);
				_gameManager.RPC_DisplayTitle(playerInfo.Key, "The servant has decided to take this role!");
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.ChangePlayerCardRole(Player, servantRole);
			_gameManager.MoveCardToCamera(Player, true);
			_gameManager.DestroyPlayerCard(_playerRevealed);
			_gameManager.DisplayTitle(null, "The servant has decided to take this role!");
#endif
			yield return new WaitForSeconds(_servantRevealDuration);

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (playerInfo.Key == Player)
				{
					_gameManager.RPC_DestroyPlayerCard(Player, _playerRevealed);
					continue;
				}

				_gameManager.RPC_PutCardBackDown(playerInfo.Key, Player, true);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.ChangePlayerCardRole(Player, RoleToTake);
			_gameManager.PutCardBackDown(Player, false);
#endif
			yield return new WaitForSeconds(_gameManager.Config.MoveToCameraDuration);
			_gameManager.SetPlayerDeathRevealCompleted();

			Destroy(gameObject);
		}

		private void OnWaitBeforeDeathRevealEnded(PlayerRef playerRevealed)
		{
			if (!_isWaitingForPromptAnswer)
			{
				return;
			}

			_gameManager.StopPromptingPlayer(Player);
		}

		private void OnDestroy()
		{
			_gameManager.WaitBeforeDeathRevealStarted -= OnWaitBeforeDeathRevealStarted;
			_gameManager.WaitBeforeDeathRevealEnded -= OnWaitBeforeDeathRevealEnded;
		}
	}
}