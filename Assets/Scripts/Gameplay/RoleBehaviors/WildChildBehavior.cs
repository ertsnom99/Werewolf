using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Data.Tags;
using Fusion;
using UnityEngine;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class WildChildBehavior : WerewolfBehavior
	{
		[Header("Wild Child")]
		[SerializeField]
		private GameplayTag _chooseModelImage;

		[SerializeField]
		private float _chooseModelMaximumDuration = 10.0f;

		[SerializeField]
		private GameplayTag _choseModelGameHistoryEntry;

		[SerializeField]
		private float _selectedModelHighlightDuration = 3.0f;

		[SerializeField]
		private GameplayTag _modelDiedGameHistoryEntry;

		private PlayerRef[] _choices;
		private IEnumerator _endRoleCallAfterTimeCoroutine;

		private PlayerRef _model;
		private bool _isModelAlive;

		public override void Initialize()
		{
			base.Initialize();

			_gameManager.PlayerDeathRevealEnded += OnPlayerDeathRevealEnded;

			if (PlayerGroups.Count < 2)
			{
				Debug.LogError("Wild Child must have two player groups: the first one for the villagers and the second one for the werewolves");
			}

			if (NightPriorities.Count < 2)
			{
				Debug.LogError("Wild Child must have two night priorities: the first one to choose his model and the second one to vote with the werewolves");
			}
		}

		public override GameplayTag[] GetCurrentPlayerGroups()
		{
			return new GameplayTag[1] { PlayerGroups[_model.IsNone || _isModelAlive ? 0 : 1] };
		}

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			if (priorityIndex == NightPriorities[0].index && _model.IsNone)
			{
				ChooseModel();
				return isWakingUp = true;
			}
			else if (priorityIndex == NightPriorities[1].index && !_model.IsNone && !_isModelAlive)
			{
				VoteForVillagers();
				return isWakingUp = true;
			}

			return isWakingUp = false;
		}

		private void ChooseModel()
		{
			List<PlayerRef> choices = _gameManager.GetAlivePlayers();
			choices.Remove(Player);

			_choices = choices.ToArray();

			if (!_gameManager.SelectPlayers(Player,
											choices,
											_chooseModelImage.CompactTagId,
											_chooseModelMaximumDuration * _gameManager.GameSpeedModifier,
											true,
											1,
											ChoicePurpose.Other,
											OnPlayersSelected))
			{
				SelectRandomModel();
				return;
			}

			_endRoleCallAfterTimeCoroutine = EndRoleCallAfterTime();
			StartCoroutine(_endRoleCallAfterTimeCoroutine);
		}

		private void OnPlayersSelected(PlayerRef[] players)
		{
			StopCoroutine(_endRoleCallAfterTimeCoroutine);
			_endRoleCallAfterTimeCoroutine = null;

			if (players == null || players.Length <= 0 || players[0].IsNone)
			{
				SelectRandomModel();
				return;
			}

			OnModelSelected(players[0]);
		}

		private void SelectRandomModel()
		{
			OnModelSelected(_choices[Random.Range(0, _choices.Length)]);
		}

		private void OnModelSelected(PlayerRef selectedModel)
		{
			_model = selectedModel;
			_isModelAlive = true;

			_gameHistoryManager.AddEntry(_choseModelGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "WildChildPlayer",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "SelectedModel",
												Data = _networkDataManager.PlayerInfos[selectedModel].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				StartCoroutine(HighlightModel());
			}
			else
			{
				StartCoroutine(WaitToStopWaitingForPlayer());
			}
		}

		private IEnumerator HighlightModel()
		{
			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, _model, true);
			_gameManager.RPC_HideUI(Player);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(_model, false);
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(_gameManager.Config.UITransitionNormalDuration + _selectedModelHighlightDuration * _gameManager.GameSpeedModifier);

			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, _model, false);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(_model, false);
#endif
			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator WaitToStopWaitingForPlayer()
		{
			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator EndRoleCallAfterTime()
		{
			float timeLeft = _chooseModelMaximumDuration * _gameManager.GameSpeedModifier;

			while (timeLeft > 0)
			{
				yield return 0;
				timeLeft -= Time.deltaTime;
			}

			_gameManager.StopSelectingPlayers(Player);
			SelectRandomModel();
		}
		private void OnPlayerDeathRevealEnded(PlayerRef deadPlayer, GameplayTag markForDeath)
		{
			if (Player.IsNone
				|| Player == deadPlayer
				|| !_gameManager.PlayerGameInfos[Player].IsAlive
				|| _model.IsNone
				|| !_isModelAlive
				|| _model != deadPlayer)
			{
				return;
			}

			_isModelAlive = false;

			_gameHistoryManager.AddEntry(_modelDiedGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "WildChildModel",
												Data = _networkDataManager.PlayerInfos[_model].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "WildChildPlayer",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			_gameManager.RemovePlayerFromGroup(Player, PlayerGroups[0]);
			_gameManager.AddPlayerToPlayerGroup(Player, PlayerGroups[1]);
		}

		public override void OnPlayerChanged()
		{
			_model = PlayerRef.None;
			_isModelAlive = false;
		}

		public override void OnRoleCallDisconnected()
		{
			if (_endRoleCallAfterTimeCoroutine != null)
			{
				SelectRandomModel();
			}

			StopAllCoroutines();
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			_gameManager.PlayerDeathRevealEnded -= OnPlayerDeathRevealEnded;
		}
	}
}
