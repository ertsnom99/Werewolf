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
		private TitleScreenData _takeRoleTitleScreen;

		[SerializeField]
		private RoleData[] _notResettedRoles;

		[SerializeField]
		private GameHistoryEntryData _tookRoleGameHistoryEntry;

		[SerializeField]
		private TitleScreenData _newRoleTitleScreen;

		[SerializeField]
		private TitleScreenData _tookThisRoleTitleScreen;

		[SerializeField]
		private float _servantRevealDuration;

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

			_gameManager.WaitBeforePlayerDeathRevealStarted += OnWaitBeforePlayerDeathRevealStarted;
			_gameManager.WaitBeforePlayerDeathRevealEnded += OnWaitBeforePlayerDeathRevealEnded;
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			return isWakingUp = false;
		}

		private void OnWaitBeforePlayerDeathRevealStarted(PlayerRef playerRevealed, MarkForDeathData mark, float revealDuration)
		{
			if (Player.IsNone
				|| !_gameManager.PlayerGameInfos[Player].IsAlive
				|| Player == playerRevealed
				|| mark != _gameManager.GameConfig.ExecutionMarkForDeath
				|| !_gameManager.PromptPlayer(Player, _takeRoleTitleScreen.ID.HashCode, revealDuration, OnTakeRole))
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

			_gameManager.TransferRole(_playerRevealed, Player, !_notResettedRoles.Contains(RoleToTake));

			_gameHistoryManager.AddEntry(_tookRoleGameHistoryEntry.ID,
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
												Data = RoleToTake.ID.HashCode.ToString(),
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
					_gameManager.RPC_FlipCard(playerInfo.Key, _playerRevealed, RoleToTake.ID.HashCode);
					_gameManager.RPC_DisplayTitle(playerInfo.Key, _newRoleTitleScreen.ID.HashCode, RoleToTake.ID.HashCode);
					continue;
				}

				if (playerInfo.Key == _playerRevealed)
				{
					_gameManager.RPC_HideUI(_playerRevealed);
				}

				_gameManager.RPC_SetRole(playerInfo.Key, _playerRevealed, -1);
				_gameManager.RPC_PutCardBackDown(playerInfo.Key, _playerRevealed, false);

				_gameManager.RPC_MoveCardToCamera(playerInfo.Key, previousPlayer, true, servantRole.ID.HashCode);
				_gameManager.RPC_DisplayTitle(playerInfo.Key, _tookThisRoleTitleScreen.ID.HashCode);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.ChangePlayerCardRole(_playerRevealed, null);
			_gameManager.PutCardBackDown(_playerRevealed, false);

			_gameManager.ChangePlayerCardRole(previousPlayer, servantRole);
			_gameManager.MoveCardToCamera(previousPlayer, true);
			_gameManager.DisplayTitle(_tookThisRoleTitleScreen.ID.HashCode);
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
			yield return new WaitForSeconds(_gameManager.GameConfig.MoveToCameraDuration);
			_gameManager.SetPlayerDeathRevealCompleted();

			Destroy(gameObject);
		}

		private void OnWaitBeforePlayerDeathRevealEnded(PlayerRef playerRevealed)
		{
			if (_isWaitingForPromptAnswer)
			{
				_gameManager.StopPromptingPlayer(Player);
			}
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.WaitBeforePlayerDeathRevealStarted -= OnWaitBeforePlayerDeathRevealStarted;
			_gameManager.WaitBeforePlayerDeathRevealEnded -= OnWaitBeforePlayerDeathRevealEnded;
		}
	}
}