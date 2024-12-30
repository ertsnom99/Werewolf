using Assets.Scripts.Data.Tags;
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
		private GameplayTag _choosePlayerImage;

		[SerializeField]
		private float _choosePlayerMaximumDuration = 10.0f;

		[SerializeField]
		private GameplayTag _choosingPlayerImage;

		[SerializeField]
		private GameplayTag _killedPlayerGameHistoryEntry;

		[SerializeField]
		private GameplayTag _markForDeathAddedByShot;

		[SerializeField]
		private float _selectedPlayerHighlightDuration = 3.0f;

		private PlayerRef[] _choices;
		private IEnumerator _startChoiceTimerCoroutine;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_gameManager.GameplayLoopStepStarts += OnGameplayLoopStepStarts;
			_gameManager.PlayerDeathRevealEnded += OnPlayerDeathRevealEnded;
			_gameManager.PostPlayerDisconnected += OnPostPlayerLeft;
		}

		public override void OnSelectedToDistribute(List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles, List<RoleData> rolesToDistribute) { }

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

		private void OnPlayerDeathRevealEnded(PlayerRef deadPlayer, GameplayTag markForDeath)
		{
			if (Player != deadPlayer || _gameManager.AlivePlayerCount <= 1)
			{
				return;
			}

			List<PlayerRef> choices = _gameManager.GetAlivePlayers();
			choices.Remove(Player);

			if (!_gameManager.SelectPlayers(Player,
											choices,
											_choosePlayerImage.CompactTagId,
											_choosePlayerMaximumDuration * _gameManager.GameSpeedModifier,
											true,
											1,
											ChoicePurpose.Kill,
											OnPlayersSelected))
			{
				if (choices.Count >= 1)
				{
					SelectRandomPlayer(choices.ToArray());
				}

				return;
			}

			_choices = choices.ToArray();

			_gameManager.WaitForPlayer(Player);

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected || playerInfo.Key == Player)
				{
					continue;
				}

				_gameManager.RPC_DisplayTitle(playerInfo.Key, _choosingPlayerImage.CompactTagId);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.DisplayTitle(_choosingPlayerImage.CompactTagId);
#endif
			_startChoiceTimerCoroutine = StartChoiceTimer(_choices);
			StartCoroutine(_startChoiceTimerCoroutine);
		}

		private IEnumerator StartChoiceTimer(PlayerRef[] choices)
		{
			float elapsedTime = .0f;

			while (elapsedTime < _choosePlayerMaximumDuration * _gameManager.GameSpeedModifier)
			{
				yield return 0;
				elapsedTime += Time.deltaTime;
			}

			_gameManager.StopSelectingPlayers(Player);
			SelectRandomPlayer(choices);
		}

		private void SelectRandomPlayer(PlayerRef[] choices)
		{
			OnPlayerSelected(choices[Random.Range(0, choices.Length)]);
		}

		private void OnPlayersSelected(PlayerRef[] players)
		{
			if (players == null || players.Length <= 0)
			{
				SelectRandomPlayer(_choices);
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
				_gameHistoryManager.AddEntry(_killedPlayerGameHistoryEntry,
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
				StartCoroutine(WaitToRemoveHighlight(selectedPlayer));
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
			yield return new WaitForSeconds(_gameManager.Config.UITransitionNormalDuration);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator WaitToRemoveHighlight(PlayerRef selectedPlayer)
		{
			yield return new WaitForSeconds(_selectedPlayerHighlightDuration * _gameManager.GameSpeedModifier);

			_gameManager.RPC_SetPlayerCardHighlightVisible(selectedPlayer, false);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(selectedPlayer, false);
#endif
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnPostPlayerLeft(PlayerRef deadPlayer)
		{
			if (deadPlayer != Player || _startChoiceTimerCoroutine == null)
			{
				return;
			}

			_gameManager.WaitForPlayer(Player);
			SelectRandomPlayer(_choices);
		}

		public override void ReInitialize() { }

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}

		private void OnDestroy()
		{
			_gameManager.GameplayLoopStepStarts -= OnGameplayLoopStepStarts;
			_gameManager.PlayerDeathRevealEnded -= OnPlayerDeathRevealEnded;
			_gameManager.PostPlayerDisconnected -= OnPostPlayerLeft;
		}
	}
}