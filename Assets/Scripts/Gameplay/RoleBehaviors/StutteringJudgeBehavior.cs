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
	public class StutteringJudgeBehavior : RoleBehavior, IVoteManagerSubscriber
	{
		[Header("Second Execution")]
		[SerializeField]
		private QuickActionScreenData _secondExecutionQuickActionScreen;

		[SerializeField]
		private GameHistoryEntryData _secondExecutionGameHistoryEntry;

		[SerializeField]
		private TitleScreenData _secondExecutionTitleScreen;

		[SerializeField]
		private float _secondExecutionTitleHoldDuration;

		private bool _canStartSecondExecution = true;
		private PlayerRef _playerWhenSecondExecutionTriggered;

		private VoteManager _voteManager;
		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			_voteManager = VoteManager.Instance;
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_voteManager.Subscribe(this);
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int priorityIndex, out bool isWakingUp)
		{
			return isWakingUp = false;
		}

		void IVoteManagerSubscriber.OnVoteStarting(ChoicePurpose purpose)
		{
			if (!_canStartSecondExecution || _gameManager.CurrentGameplayLoopStep != GameplayLoopStep.Execution)
			{
				return;
			}

			_voteManager.VoteCompleted += OnVoteEnded;
			_gameManager.DisplayQuickAction(Player, _secondExecutionQuickActionScreen.ID.HashCode, OnTriggerSecondExecution);
		}

		private void OnTriggerSecondExecution()
		{
			_voteManager.VoteCompleted -= OnVoteEnded;
			_gameManager.HideQuickAction(Player);
			_canStartSecondExecution = false;
			_playerWhenSecondExecutionTriggered = Player;
			_gameManager.PreChangeGameplayLoopStep += OnPreChangeGameplayLoopStep;
		}

		private void OnPreChangeGameplayLoopStep()
		{
			if (_gameManager.CurrentGameplayLoopStep != GameplayLoopStep.ExecutionWinnerCheck || _gameManager.AlivePlayerCount <= 1)
			{
				return;
			}

			_gameManager.PreChangeGameplayLoopStep -= OnPreChangeGameplayLoopStep;

			_gameManager.SetNextGameplayLoopStep(GameplayLoopStep.Execution);

			_gameHistoryManager.AddEntry(_secondExecutionGameHistoryEntry.ID,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "Player",
												Data = _networkDataManager.PlayerInfos[_playerWhenSecondExecutionTriggered].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			_playerWhenSecondExecutionTriggered = PlayerRef.None;
			
			_gameManager.WaitForPlayer(Player);

			_gameManager.RPC_DisplayTitle(_secondExecutionTitleScreen.ID.HashCode);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.DisplayTitle(_secondExecutionTitleScreen.ID.HashCode);
#endif
			StartCoroutine(WaitToHideSecondExecutionTitle());
		}

		private IEnumerator WaitToHideSecondExecutionTitle()
		{
			GameConfig gameConfig = _gameManager.GameConfig;

			yield return new WaitForSeconds(gameConfig.UITransitionNormalDuration + _secondExecutionTitleHoldDuration * _gameManager.GameSpeedModifier);

			_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(gameConfig.UITransitionNormalDuration);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnVoteEnded(Dictionary<PlayerRef, int> votes)
		{
			_voteManager.VoteCompleted -= OnVoteEnded;
			_gameManager.HideQuickAction(Player);
		}

		public override void OnPlayerChanged()
		{
			_canStartSecondExecution = true;
		}

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_voteManager.Unsubscribe(this);
			_voteManager.VoteCompleted -= OnVoteEnded;
			_gameManager.PreChangeGameplayLoopStep -= OnPreChangeGameplayLoopStep;
		}
	}
}
