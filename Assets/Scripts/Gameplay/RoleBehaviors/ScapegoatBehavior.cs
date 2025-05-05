using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;
using static Werewolf.Managers.GameManager;

namespace Werewolf.Gameplay.Role
{
	public class ScapegoatBehavior : RoleBehavior
	{
		[Header("Execution Draw")]
		[SerializeField]
		private GameHistoryEntryData _executionDrawGameHistoryEntry;

		[SerializeField]
		private TitleScreenData _executionDrawTitleScreen;

		[SerializeField]
		private TitleScreenData _roleRevealTitleScreen;

		[Header("Choose Next Voters")]
		[SerializeField]
		private TitleScreenData _choosePlayersTitleScreen;

		[SerializeField]
		private float _choosePlayersMaximumDuration;

		[SerializeField]
		private TitleScreenData _choosingPlayersTitleScreen;

		[SerializeField]
		private GameHistoryEntryData _nextVotersGameHistoryEntry;

		[SerializeField]
		private float _selectedPlayersHighlightDuration;

		private bool _executionDrawHappened;
		private PlayerRef[] _choices;
		private IEnumerator _startChoiceTimerCoroutine;
		private PlayerRef[] _nextVoters;

		private NetworkDataManager _networkDataManager;
		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private VoteManager _voteManager;

		public override void Initialize()
		{
			_networkDataManager = NetworkDataManager.Instance;
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_voteManager = VoteManager.Instance;

			_gameManager.FirstExecutionVotesCounted += OnFirstExecutionVotesCounted;
			_gameManager.PlayerDeathRevealStarted += OnPlayerDeathRevealStarted;
			_gameManager.WaitBeforePlayerDeathRevealEnded += OnWaitBeforePlayerDeathRevealEnded;
			_gameManager.PlayerDeathRevealEnded += OnPlayerDeathRevealEnded;
			_voteManager.VoteStarting += OnVoteStarting;
			_gameManager.GameplayLoopStepStarts += OnGameplayLoopStepStarts;
			_gameManager.PostPlayerDisconnected += OnPostPlayerLeft;
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			return isWakingUp = false;
		}

		private void OnFirstExecutionVotesCounted(List<PlayerRef> mostVotedPlayers)
		{
			if (Player == PlayerRef.None || !_gameManager.PlayerGameInfos[Player].IsAlive || mostVotedPlayers.Count <= 1)
			{
				return;
			}

			_gameHistoryManager.AddEntry(_executionDrawGameHistoryEntry.ID,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "ScapegoatPlayer",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			mostVotedPlayers.Clear();
			mostVotedPlayers.Add(Player);

			_executionDrawHappened = true;
		}

		private void OnPlayerDeathRevealStarted(PlayerRef playerRevealed)
		{
			if (_networkDataManager.PlayerInfos[Player].IsConnected && Player == playerRevealed && _executionDrawHappened)
			{
				_gameManager.RPC_DisplayTitle(Player, _executionDrawTitleScreen.ID.HashCode);
			}
		}

		private void OnWaitBeforePlayerDeathRevealEnded(PlayerRef playerRevealed)
		{
			if (Player != playerRevealed || !_executionDrawHappened)
			{
				return;
			}

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected && playerInfo.Key != Player)
				{
					_gameManager.RPC_DisplayTitle(playerInfo.Key, _roleRevealTitleScreen.ID.HashCode);
				}
			}
		}

		private void OnPlayerDeathRevealEnded(PlayerRef deadPlayer, MarkForDeathData markForDeath)
		{
			if (Player != deadPlayer || !_executionDrawHappened || _gameManager.AlivePlayerCount <= 1)
			{
				return;
			}

			List<PlayerRef> choices = _gameManager.GetAlivePlayers();
			choices.Remove(Player);

			_choices = choices.ToArray();

			if (!_gameManager.SelectPlayers(Player,
											choices,
											_choosePlayersTitleScreen.ID.HashCode,
											_choosePlayersMaximumDuration * _gameManager.GameSpeedModifier,
											true,
											-1,
											ChoicePurpose.Other,
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
					_gameManager.RPC_DisplayTitle(playerInfo.Key, _choosingPlayersTitleScreen.ID.HashCode);
				}
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.DisplayTitle(_choosingPlayersTitleScreen.ID.HashCode);
#endif
			_startChoiceTimerCoroutine = StartChoiceTimer();
			StartCoroutine(_startChoiceTimerCoroutine);
		}

		private IEnumerator StartChoiceTimer()
		{
			float elapsedTime = .0f;

			while (elapsedTime < _choosePlayersMaximumDuration * _gameManager.GameSpeedModifier)
			{
				yield return 0;
				elapsedTime += Time.deltaTime;
			}

			_gameManager.StopSelectingPlayers(Player);
			SelectRandomPlayer();
		}

		private void SelectRandomPlayer()
		{
			OnPlayersSelected(new PlayerRef[1] { _choices[Random.Range(0, _choices.Length)] });
		}

		private void OnPlayersSelected(PlayerRef[] selectedPlayers)
		{
			if (selectedPlayers == null || selectedPlayers.Length <= 0)
			{
				SelectRandomPlayer();
				return;
			}

			if (_startChoiceTimerCoroutine != null)
			{
				StopCoroutine(_startChoiceTimerCoroutine);
				_startChoiceTimerCoroutine = null;
			}

			_gameHistoryManager.AddEntry(_nextVotersGameHistoryEntry.ID,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "ScapegoatPlayer",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "NextVotersPlayers",
												Data = ConcatenatePlayersNickname(selectedPlayers, _networkDataManager),
												Type = GameHistorySaveEntryVariableType.Players
											}
										});

			_nextVoters = selectedPlayers;
			_executionDrawHappened = false;

			foreach (PlayerRef selectedPlayer in selectedPlayers)
			{
				_gameManager.RPC_SetPlayerCardHighlightVisible(selectedPlayer, true);
			}

			_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			foreach (PlayerRef selectedPlayer in selectedPlayers)
			{
				_gameManager.SetPlayerCardHighlightVisible(selectedPlayer, true);
			}

			_gameManager.HideUI();
#endif
			StartCoroutine(WaitToRemoveHighlight(selectedPlayers));
			return;
		}

		private IEnumerator WaitToRemoveHighlight(PlayerRef[] selectedPlayers)
		{
			yield return new WaitForSeconds(_selectedPlayersHighlightDuration * _gameManager.GameSpeedModifier);

			foreach (PlayerRef selectedPlayer in selectedPlayers)
			{
				_gameManager.RPC_SetPlayerCardHighlightVisible(selectedPlayer, false);
			}
#if UNITY_SERVER && UNITY_EDITOR
			foreach (PlayerRef selectedPlayer in selectedPlayers)
			{
				_gameManager.SetPlayerCardHighlightVisible(selectedPlayer, false);
			}
#endif
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnVoteStarting(ChoicePurpose purpose)
		{
			if (_gameManager.CurrentGameplayLoopStep != GameplayLoopStep.Execution || _nextVoters == null)
			{
				return;
			}

			_voteManager.ClearVoters();
			_voteManager.ClearSpectators();

			foreach (PlayerRef nextVoter in _nextVoters)
			{
				if (!_gameManager.PlayerGameInfos[nextVoter].IsAlive)
				{
					continue;
				}

				_voteManager.AddVoter(nextVoter);
				_voteManager.AddVoteImmunity(nextVoter, nextVoter);
			}

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (!playerInfo.Value.IsAlive || !_voteManager.Voters.Contains(playerInfo.Key))
				{
					_voteManager.AddSpectator(playerInfo.Key);
				}
			}
		}

		private void OnGameplayLoopStepStarts(GameplayLoopStep gameplayLoopStep)
		{
			if (_nextVoters != null && gameplayLoopStep == GameplayLoopStep.ExecutionDeathReveal)
			{
				_nextVoters = null;
			}
		}

		private void OnPostPlayerLeft(PlayerRef deadPlayer)
		{
			if (deadPlayer != Player || _startChoiceTimerCoroutine == null)
			{
				return;
			}

			_gameManager.WaitForPlayer(Player);
			SelectRandomPlayer();
		}

		public override void OnPlayerChanged()
		{
			_executionDrawHappened = false;
			_nextVoters = null;
		}

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.FirstExecutionVotesCounted -= OnFirstExecutionVotesCounted;
			_gameManager.PlayerDeathRevealStarted -= OnPlayerDeathRevealStarted;
			_gameManager.WaitBeforePlayerDeathRevealEnded -= OnWaitBeforePlayerDeathRevealEnded;
			_gameManager.PlayerDeathRevealEnded -= OnPlayerDeathRevealEnded;
			_gameManager.PostPlayerDisconnected -= OnPostPlayerLeft;
			_voteManager.VoteStarting -= OnVoteStarting;
			_gameManager.GameplayLoopStepStarts -= OnGameplayLoopStepStarts;
		}
	}
}
