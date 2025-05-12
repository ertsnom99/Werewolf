using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;
using static Werewolf.Managers.GameManager;

namespace Werewolf.Gameplay.Role
{
	public class HunterBehavior : RoleBehavior
	{
		[Header("Shoot Player")]
		[SerializeField]
		private TitleScreenData _choosePlayerTitleScreen;

		[SerializeField]
		private float _choosePlayerMaximumDuration;

		[SerializeField]
		private TitleScreenData _choosingPlayerTitleScreen;

		[SerializeField]
		private GameHistoryEntryData _killedPlayerGameHistoryEntry;

		[SerializeField]
		private MarkForDeathData _markForDeathAddedByShot;

		[SerializeField]
		private float _selectedPlayerHighlightDuration;

		private PlayerRef[] _choices;
		private IEnumerator _startChoiceTimerCoroutine;
		private IEnumerator _waitToRemoveHighlightCoroutine;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_gameManager.GameplayLoopStepStarts += OnGameplayLoopStepStarts;
			_gameManager.PlayerDied += OnPlayerDied;
			_gameManager.PostPlayerDisconnected += OnPostPlayerDisconnected;
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			return isWakingUp = false;
		}

		private void OnGameplayLoopStepStarts(GameplayLoopStep gameplayLoopStep)
		{
			if (gameplayLoopStep == GameplayLoopStep.DayDeathReveal)
			{
				_gameManager.MoveMarksForDeathToLast(Player);
			}
		}

		private void OnPlayerDied(PlayerRef deadPlayer, MarkForDeathData markForDeath)
		{
			if (Player != deadPlayer || _gameManager.AlivePlayerCount <= 1)
			{
				return;
			}

			List<PlayerRef> choices = _gameManager.GetAlivePlayers();
			_choices = choices.ToArray();

			if (!_gameManager.SelectPlayers(Player,
											choices,
											_choosePlayerTitleScreen.ID.HashCode,
											_choosePlayerMaximumDuration * _gameManager.GameSpeedModifier,
											true,
											1,
											ChoicePurpose.Kill,
											OnPlayersSelected))
			{
				if (choices.Count > 0)
				{
					_gameManager.WaitForPlayer(Player);
					SelectRandomPlayer();
				}

				return;
			}

			_gameManager.WaitForPlayer(Player);

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected && playerInfo.Key != Player)
				{
					_gameManager.RPC_DisplayTitle(playerInfo.Key, _choosingPlayerTitleScreen.ID.HashCode);
				}
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.DisplayTitle(_choosingPlayerTitleScreen.ID.HashCode);
#endif
			_startChoiceTimerCoroutine = StartChoiceTimer();
			StartCoroutine(_startChoiceTimerCoroutine);
		}

		private IEnumerator StartChoiceTimer()
		{
			float elapsedTime = .0f;

			while (elapsedTime < _choosePlayerMaximumDuration * _gameManager.GameSpeedModifier)
			{
				yield return 0;
				elapsedTime += Time.deltaTime;
			}

			_gameManager.StopSelectingPlayers(Player);
			SelectRandomPlayer();
		}

		private void SelectRandomPlayer()
		{
			OnPlayerSelected(_choices[Random.Range(0, _choices.Length)]);
		}

		private void OnPlayersSelected(PlayerRef[] players)
		{
			if (players == null || players.Length <= 0)
			{
				SelectRandomPlayer();
				return;
			}

			OnPlayerSelected(players[0]);
		}

		private void OnPlayerSelected(PlayerRef selectedPlayer)
		{
			if (_startChoiceTimerCoroutine != null)
			{
				StopCoroutine(_startChoiceTimerCoroutine);
				_startChoiceTimerCoroutine = null;
			}

			if (!selectedPlayer.IsNone)
			{
				_gameHistoryManager.AddEntry(_killedPlayerGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "HunterPlayer",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												},
												new()
												{
													Name = "KilledPlayer",
													Data = _networkDataManager.PlayerInfos[selectedPlayer].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												}
											});

				_gameManager.AddMarkForDeath(selectedPlayer, _markForDeathAddedByShot, 1);
				_gameManager.RPC_SetPlayerCardHighlightVisible(selectedPlayer, true);
				_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
				_gameManager.SetPlayerCardHighlightVisible(selectedPlayer, true);
				_gameManager.HideUI();
#endif
				_waitToRemoveHighlightCoroutine = WaitToRemoveHighlight(selectedPlayer);
				StartCoroutine(_waitToRemoveHighlightCoroutine);
				return;
			}

			StartCoroutine(HideUIBeforeStopWaintingForPlayer());
		}

		private IEnumerator HideUIBeforeStopWaintingForPlayer()
		{
			_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(_gameManager.GameConfig.UITransitionNormalDuration);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator WaitToRemoveHighlight(PlayerRef selectedPlayer)
		{
			yield return new WaitForSeconds(_selectedPlayerHighlightDuration * _gameManager.GameSpeedModifier);

			_waitToRemoveHighlightCoroutine = null;

			_gameManager.RPC_SetPlayerCardHighlightVisible(selectedPlayer, false);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(selectedPlayer, false);
#endif
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnPostPlayerDisconnected(PlayerRef deadPlayer)
		{
			if (deadPlayer != Player || (_startChoiceTimerCoroutine == null && _waitToRemoveHighlightCoroutine == null))
			{
				return;
			}

			_gameManager.WaitForPlayer(Player);

			if (_startChoiceTimerCoroutine != null)
			{
				SelectRandomPlayer();
			}
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.GameplayLoopStepStarts -= OnGameplayLoopStepStarts;
			_gameManager.PlayerDied -= OnPlayerDied;
			_gameManager.PostPlayerDisconnected -= OnPostPlayerDisconnected;
		}
	}
}