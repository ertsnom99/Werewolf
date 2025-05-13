using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Utilities.GameplayData;
using Werewolf.Data;
using Werewolf.Managers;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class WildChildBehavior : WerewolfBehavior, IGameManagerSubscriber
	{
		[Header("Wild Child")]
		[SerializeField]
		private TitleScreenData _chooseModelTitleScreen;

		[SerializeField]
		private float _chooseModelMaximumDuration;

		[SerializeField]
		private GameHistoryEntryData _choseModelGameHistoryEntry;

		[SerializeField]
		private float _selectedModelHighlightDuration;

		[SerializeField]
		private GameHistoryEntryData _modelDiedGameHistoryEntry;

		private PlayerRef[] _choices;
		private IEnumerator _endRoleCallAfterTimeCoroutine;
		private PlayerRef _model;
		private bool _isModelAlive;

		public override void Initialize()
		{
			base.Initialize();

			_gameManager.Subscribe(this);

			if (PlayerGroupIDs.Count < 2)
			{
				Debug.LogError($"{nameof(WildChildBehavior)} must have two player groups: the first one for the villagers and the second one for the werewolves");
			}

			if (NightPriorities.Count < 2)
			{
				Debug.LogError($"{nameof(WildChildBehavior)} must have two night priorities: the first one to choose his model and the second one to vote with the werewolves");
			}
		}

		public override UniqueID[] GetCurrentPlayerGroupIDs()
		{
			return new UniqueID[1] { PlayerGroupIDs[_model.IsNone || _isModelAlive ? 0 : 1] };
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
				VoteForVillager();
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
											_chooseModelTitleScreen.ID.HashCode,
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

			_gameHistoryManager.AddEntry(_choseModelGameHistoryEntry.ID,
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
			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_HideUI(Player);
				_gameManager.RPC_SetPlayerCardHighlightVisible(Player, _model, true);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(_model, false);
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(_gameManager.GameConfig.UITransitionNormalDuration + _selectedModelHighlightDuration * _gameManager.GameSpeedModifier);

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

		void IGameManagerSubscriber.OnPlayerDied(PlayerRef deadPlayer, MarkForDeathData markForDeath)
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

			_gameHistoryManager.AddEntry(_modelDiedGameHistoryEntry.ID,
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

			_gameManager.RemovePlayerFromPlayerGroup(Player, PlayerGroupIDs[0]);
			_gameManager.AddPlayerToPlayerGroup(Player, PlayerGroupIDs[1]);
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

			_gameManager.Unsubscribe(this);
		}
	}
}
