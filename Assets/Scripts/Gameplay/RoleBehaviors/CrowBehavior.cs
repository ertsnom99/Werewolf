using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using System.Collections;
using static Werewolf.Managers.GameHistoryManager;
using static Werewolf.Managers.GameManager;

namespace Werewolf.Gameplay.Role
{
	public class CrowBehavior : RoleBehavior, IVoteManagerSubscriber
	{
		[Header("Choose Player")]
		[SerializeField]
		private TitleScreenData _choosePlayerTitleScreen;

		[SerializeField]
		private float _choosePlayerMaximumDuration;

		[SerializeField]
		private GameHistoryEntryData _chosePlayerGameHistoryEntry;

		[SerializeField]
		private float _choosenPlayerHighlightDuration;

		[Header("Marker")]
		[SerializeField]
		private MarkerData _markerData;

		[SerializeField]
		private Vector3 _markerOffset;

		private IEnumerator _endRoleCallAfterTimeCoroutine;
		private PlayerRef _choosenPlayer;
		private bool _markerIdInstantiated;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;
		private VoteManager _voteManager;

		private readonly int EXTRA_VOTE_AMOUNT = 2;

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;
			_voteManager = VoteManager.Instance;

			_gameManager.GameplayLoopStepStarts += OnGameplayLoopStepStarts;
			_gameManager.PlayerDeathRevealStarted += OnPlayerDeathRevealStarted;
			_voteManager.Subscribe(this);
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int priorityIndex, out bool isWakingUp)
		{
			List<PlayerRef> choices = _gameManager.GetAlivePlayers();
			choices.Remove(Player);

			if (!_gameManager.SelectPlayers(Player,
											choices,
											_choosePlayerTitleScreen.ID.HashCode,
											_choosePlayerMaximumDuration * _gameManager.GameSpeedModifier,
											false,
											1,
											ChoicePurpose.Kill,
											OnPlayerSelected))
			{
				StartCoroutine(WaitToStopWaitingForPlayer());

				isWakingUp = false;
				return true;
			}

			_endRoleCallAfterTimeCoroutine = EndRoleCallAfterTime();
			StartCoroutine(_endRoleCallAfterTimeCoroutine);

			return isWakingUp = true;
		}

		private IEnumerator WaitToStopWaitingForPlayer()
		{
			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnPlayerSelected(PlayerRef[] players)
		{
			StopCoroutine(_endRoleCallAfterTimeCoroutine);

			if (players == null || players.Length <= 0 || players[0].IsNone)
			{
				_gameManager.StopWaintingForPlayer(Player);
				return;
			}

			_choosenPlayer = players[0];

			_gameHistoryManager.AddEntry(_chosePlayerGameHistoryEntry.ID,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "CrowPlayer",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "ChosenPlayer",
												Data = _networkDataManager.PlayerInfos[_choosenPlayer].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				StartCoroutine(HighlightChoosenPlayer());
			}
			else
			{
				StartCoroutine(WaitToStopWaitingForPlayer());
			}
		}

		private IEnumerator HighlightChoosenPlayer()
		{
			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_HideUI(Player);
				_gameManager.RPC_SetPlayerCardHighlightVisible(Player, _choosenPlayer, true);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(_choosenPlayer, true);
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(_gameManager.GameConfig.UITransitionNormalDuration + _choosenPlayerHighlightDuration * _gameManager.GameSpeedModifier);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator EndRoleCallAfterTime()
		{
			float timeLeft = _choosePlayerMaximumDuration * _gameManager.GameSpeedModifier;

			while (timeLeft > 0)
			{
				yield return 0;
				timeLeft -= Time.deltaTime;
			}

			_gameManager.StopSelectingPlayers(Player);
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnGameplayLoopStepStarts(GameplayLoopStep gameplayLoopStep)
		{
			if (!_choosenPlayer.IsNone && gameplayLoopStep == GameplayLoopStep.DayTransition)
			{
				CreateMarker();
			}
			else if (_markerIdInstantiated && gameplayLoopStep == GameplayLoopStep.ExecutionDeathReveal)
			{
				DestroyMarker();
			}
		}

		private void OnPlayerDeathRevealStarted(PlayerRef deadPlayer, MarkForDeathData markForDeath)
		{
			if (_markerIdInstantiated && !_choosenPlayer.IsNone && _choosenPlayer == deadPlayer)
			{
				DestroyMarker();
			}
		}

		private void CreateMarker()
		{
			_gameManager.RPC_InstantiateMarker(_markerData.ID.HashCode, _choosenPlayer, _markerOffset);
			_markerIdInstantiated = true;
		}

		private void DestroyMarker()
		{
			_gameManager.RPC_DestroyMarker(_markerData.ID.HashCode);
			_markerIdInstantiated = false;
		}

		void IVoteManagerSubscriber.OnVoteStarting(ChoicePurpose purpose)
		{
			if (_choosenPlayer.IsNone || _gameManager.CurrentGameplayLoopStep != GameplayLoopStep.Execution)
			{
				return;
			}

			_voteManager.AddExtraVote(_choosenPlayer, EXTRA_VOTE_AMOUNT);
			_choosenPlayer = PlayerRef.None;
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}

		private void OnDestroy()
		{
			_gameManager.GameplayLoopStepStarts -= OnGameplayLoopStepStarts;
			_gameManager.PlayerDeathRevealStarted -= OnPlayerDeathRevealStarted;
			_voteManager.Unsubscribe(this);
		}
	}
}
