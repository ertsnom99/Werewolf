using Assets.Scripts.Data.Tags;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;

namespace Werewolf
{
	public class ServantBehavior : RoleBehavior
	{
		[Header("Take Role")]
		[SerializeField]
		private string _takeRolePrompt;

		[SerializeField]
		private string _confirmTakeRoleButtonText;

		[SerializeField]
		private string _newRoleText;

		[SerializeField]
		private string _tookThisRoleText;

		[SerializeField]
		private float _servantRevealDuration = 3.0f;

		private bool _isWaitingForPromptAnswer;
		private PlayerRef _playerRevealed;

		private NetworkDataManager _networkDataManager;
		private GameManager _gameManager;

		public override void Init()
		{
			_networkDataManager = NetworkDataManager.Instance;
			_gameManager = GameManager.Instance;

			_gameManager.WaitBeforeDeathRevealStarted += OnWaitBeforeDeathRevealStarted;
			_gameManager.WaitBeforeDeathRevealEnded += OnWaitBeforeDeathRevealEnded;
		}

		public override void OnSelectedToDistribute(ref List<RoleData> rolesToDistribute, ref List<RoleSetupData> availableRoles) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex)
		{
			return false;
		}

		private void OnWaitBeforeDeathRevealStarted(PlayerRef playerRevealed, List<GameplayTag> marks, float revealDuration)
		{
			if (Player.IsNone
				|| !_gameManager.PlayerGameInfos[Player].IsAlive
				|| Player == playerRevealed
				|| !marks.Contains(_gameManager.Config.ExecutionMarkForDeath)
				|| !_gameManager.PromptPlayer(Player, _takeRolePrompt, revealDuration, _confirmTakeRoleButtonText, OnTakeRole))
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
			PlayerRef previousPlayer = Player;

			_gameManager.TransferRole(_playerRevealed, Player, false);

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected)
				{
					continue;
				}

				if (playerInfo.Key == previousPlayer)
				{
					_gameManager.RPC_FlipCard(playerInfo.Key, _playerRevealed, RoleToTake.GameplayTag.CompactTagId);
					_gameManager.RPC_DisplayTitle(playerInfo.Key, string.Format(_newRoleText, RoleToTake.Name));
					continue;
				}

				if (playerInfo.Key == _playerRevealed)
				{
					_gameManager.RPC_HideUI(_playerRevealed);
				}

				_gameManager.RPC_MoveCardToCamera(playerInfo.Key, previousPlayer, true, servantRole.GameplayTag.CompactTagId);
				_gameManager.RPC_DestroyPlayerCard(playerInfo.Key, _playerRevealed);
				_gameManager.RPC_DisplayTitle(playerInfo.Key, _tookThisRoleText);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.ChangePlayerCardRole(previousPlayer, servantRole);
			_gameManager.MoveCardToCamera(previousPlayer, true);
			_gameManager.DestroyPlayerCard(_playerRevealed);
			_gameManager.DisplayTitle(null, _tookThisRoleText);
#endif
			yield return new WaitForSeconds(_servantRevealDuration);

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected)
				{
					continue;
				}

				if (playerInfo.Key == previousPlayer)
				{
					_gameManager.RPC_DestroyPlayerCard(previousPlayer, _playerRevealed);
					continue;
				}

				_gameManager.RPC_PutCardBackDown(playerInfo.Key, previousPlayer, true);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.ChangePlayerCardRole(previousPlayer, RoleToTake);
			_gameManager.PutCardBackDown(previousPlayer, false);
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

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.WaitBeforeDeathRevealStarted -= OnWaitBeforeDeathRevealStarted;
			_gameManager.WaitBeforeDeathRevealEnded -= OnWaitBeforeDeathRevealEnded;
		}
	}
}