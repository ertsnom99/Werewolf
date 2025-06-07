using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using Utilities.GameplayData;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;
using static Werewolf.Managers.GameManager;

namespace Werewolf.Gameplay.Role
{
	public class AngelBehavior : RoleBehavior
	{
		[Header("Starting Execution")]
		[SerializeField]
		private GameHistoryEntryData _angelStartingGameWithExecutionGameHistoryEntry;

		[SerializeField]
		private TitleScreenData _angelStartingGameWithExecutionTitleScreen;

		[SerializeField]
		private float _angelStartingGameWithExecutionTitleHoldDuration;

		[Header("Win Condition")]
		[SerializeField]
		private MarkForDeathData[] _marksForDeathToWin;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			if (PlayerGroupIDs.Count < 2)
			{
				Debug.LogError($"{nameof(AngelBehavior)} must have two player groups: the first one for the villagers and the second one for the angel");
			}

			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_gameManager.PreChangeGameplayLoopStep += OnPreChangeGameplayLoopStep;
			_gameManager.GameplayLoopStepStarts += OnGameplayLoopStepStarts;
			_gameManager.RevealDeadPlayerRoleEnded += OnRevealDeadPlayerRoleEnded;
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int priorityIndex, out bool isWakingUp)
		{
			return isWakingUp = false;
		}

		private void OnPreChangeGameplayLoopStep()
		{
			if (Player.IsNone)
			{
				return;
			}
			else if (_gameManager.CurrentGameplayLoopStep == GameplayLoopStep.Election)
			{
				_gameManager.SetNextGameplayLoopStep(GameplayLoopStep.ExecutionDebate);

				_gameHistoryManager.AddEntry(_angelStartingGameWithExecutionGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "Player",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
											});

				_gameManager.WaitForPlayer(Player);

				_gameManager.RPC_DisplayTitle(_angelStartingGameWithExecutionTitleScreen.ID.HashCode);
#if UNITY_SERVER && UNITY_EDITOR
				_gameManager.DisplayTitle(_angelStartingGameWithExecutionTitleScreen.ID.HashCode);
#endif
				StartCoroutine(WaitToHideAngelStartingGameWithExecutionTitle());
			}
			else if (_gameManager.CurrentGameplayLoopStep == GameplayLoopStep.DayWinnerCheck)
			{
				_gameManager.RemovePlayerFromPlayerGroup(Player, PlayerGroupIDs[1]);

				_gameManager.PreChangeGameplayLoopStep -= OnPreChangeGameplayLoopStep;
				_gameManager.GameplayLoopStepStarts -= OnGameplayLoopStepStarts;
				_gameManager.RevealDeadPlayerRoleEnded -= OnRevealDeadPlayerRoleEnded;
			}
		}

		private IEnumerator WaitToHideAngelStartingGameWithExecutionTitle()
		{
			GameConfig gameConfig = _gameManager.GameConfig;

			yield return new WaitForSeconds(gameConfig.UITransitionNormalDuration + _angelStartingGameWithExecutionTitleHoldDuration * _gameManager.GameSpeedModifier);

			_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(gameConfig.UITransitionNormalDuration);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnGameplayLoopStepStarts(GameplayLoopStep currentGameplayLoopStep)
		{
			if (!Player.IsNone
				&& currentGameplayLoopStep == GameplayLoopStep.ExecutionWinnerCheck
				&& _gameManager.IsPlayerInPlayerGroup(Player, PlayerGroupIDs[0]))
			{
				_gameManager.EndGameplayLoopStep();
			}
		}

		private void OnRevealDeadPlayerRoleEnded(PlayerRef deadPlayer, MarkForDeathData markForDeath)
		{
			if (deadPlayer != Player || !_marksForDeathToWin.Contains(markForDeath))
			{
				return;
			}

			_gameManager.RemoveAllMarksForDeath(Player);
			_gameManager.RemovePlayerFromAllPlayerGroups(Player, new UniqueID[1] { PlayerGroupIDs[1] });

			foreach(KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (playerInfo.Key != Player)
				{
					_gameManager.RemovePlayerFromAllPlayerGroups(playerInfo.Key);
				}
			}

			_gameManager.StopDeathReveal();
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.PreChangeGameplayLoopStep -= OnPreChangeGameplayLoopStep;
			_gameManager.GameplayLoopStepStarts -= OnGameplayLoopStepStarts;
			_gameManager.RevealDeadPlayerRoleEnded -= OnRevealDeadPlayerRoleEnded;
		}
	}
}
