using Assets.Scripts.Data.Tags;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class ServantBehavior : RoleBehavior
	{
		[Header("Take Role")]
		[SerializeField]
		private GameplayTag _takeRoleImage;

		[SerializeField]
		private GameplayTag[] _notResettedRoles;

		[SerializeField]
		private GameplayTag _tookRoleGameHistoryEntry;

		[SerializeField]
		private GameplayTag _newRoleImage;

		[SerializeField]
		private GameplayTag _tookThisRoleImage;

		[SerializeField]
		private float _servantRevealDuration = 3.0f;

		private bool _isWaitingForPromptAnswer;
		private PlayerRef _playerRevealed;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_gameManager.WaitBeforeDeathRevealStarted += OnWaitBeforeDeathRevealStarted;
			_gameManager.WaitBeforeDeathRevealEnded += OnWaitBeforeDeathRevealEnded;
		}

		public override void OnSelectedToDistribute(List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			return isWakingUp = false;
		}

		private void OnWaitBeforeDeathRevealStarted(PlayerRef playerRevealed, List<GameplayTag> marks, float revealDuration)
		{
			if (Player.IsNone
				|| !_gameManager.PlayerGameInfos[Player].IsAlive
				|| Player == playerRevealed
				|| !marks.Contains(_gameManager.Config.ExecutionMarkForDeath)
				|| !_gameManager.PromptPlayer(Player, _takeRoleImage.CompactTagId, revealDuration, OnTakeRole))
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

			_gameManager.TransferRole(_playerRevealed, Player, !_notResettedRoles.Contains(RoleToTake.GameplayTag));

			_gameHistoryManager.AddEntry(_tookRoleGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "ServantPlayer",
												Data = _networkDataManager.PlayerInfos[previousPlayer].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "PlayerTakenFrom",
												Data = _networkDataManager.PlayerInfos[_playerRevealed].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "RoleName",
												Data = RoleToTake.GameplayTag.name,
												Type = GameHistorySaveEntryVariableType.RoleName
											}
										});

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected)
				{
					continue;
				}

				if (playerInfo.Key == previousPlayer)
				{
					_gameManager.RPC_FlipCard(playerInfo.Key, _playerRevealed, RoleToTake.GameplayTag.CompactTagId);
					_gameManager.RPC_DisplayTitle(playerInfo.Key, _newRoleImage.CompactTagId, RoleToTake.GameplayTag.CompactTagId);
					continue;
				}

				if (playerInfo.Key == _playerRevealed)
				{
					_gameManager.RPC_HideUI(_playerRevealed);
				}

				_gameManager.RPC_SetRole(playerInfo.Key, _playerRevealed, -1);
				_gameManager.RPC_PutCardBackDown(playerInfo.Key, _playerRevealed, false);

				_gameManager.RPC_MoveCardToCamera(playerInfo.Key, previousPlayer, true, servantRole.GameplayTag.CompactTagId);
				_gameManager.RPC_DisplayTitle(playerInfo.Key, _tookThisRoleImage.CompactTagId);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.ChangePlayerCardRole(_playerRevealed, null);
			_gameManager.PutCardBackDown(_playerRevealed, false);

			_gameManager.ChangePlayerCardRole(previousPlayer, servantRole);
			_gameManager.MoveCardToCamera(previousPlayer, true);
			_gameManager.DisplayTitle(_tookThisRoleImage.CompactTagId);
#endif
			yield return new WaitForSeconds(_servantRevealDuration * _gameManager.GameSpeedModifier);

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected)
				{
					continue;
				}

				if (playerInfo.Key == previousPlayer)
				{
					_gameManager.RPC_SetRole(previousPlayer, _playerRevealed, -1);
					_gameManager.RPC_PutCardBackDown(previousPlayer, _playerRevealed, false);

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

		public override void ReInitialize() { }

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.WaitBeforeDeathRevealStarted -= OnWaitBeforeDeathRevealStarted;
			_gameManager.WaitBeforeDeathRevealEnded -= OnWaitBeforeDeathRevealEnded;
		}
	}
}