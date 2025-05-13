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
	public class KnightWithRustySwordBehavior : RoleBehavior, IGameManagerSubscriber
	{
		[Header("Kill Werewolf")]
		[SerializeField]
		private MarkForDeathData[] _markForDeathToKillWerewolf;

		[SerializeField]
		private PlayerGroupData[] _werewolvesPlayerGroups;

		[SerializeField]
		private GameHistoryEntryData _gaveTetanusGameHistoryEntry;

		[SerializeField]
		private MarkForDeathData _markForDeathAddedByStab;

		private UniqueID[] _werewolvesPlayerGroupIDs;
		private PlayerRef _werewolfToKill;
		private bool _killWerewolf;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			_werewolvesPlayerGroupIDs = GameplayData.GetIDs(_werewolvesPlayerGroups);

			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_gameManager.MarkForDeathAdded += OnMarkForDeathAdded;
			_gameManager.Subscribe(this);
			_gameManager.GameplayLoopStepStarts += OnGameplayLoopStepStarts;
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			return isWakingUp = false;
		}

		private void OnMarkForDeathAdded(PlayerRef player, MarkForDeathData markForDeath)
		{
			if (player == Player && _markForDeathToKillWerewolf.Contains(markForDeath))
			{
				_werewolfToKill = _gameManager.FindNextPlayer(Player, searchToLeft: true, mustBeAwake: true, _werewolvesPlayerGroupIDs);
			}
		}

		void IGameManagerSubscriber.OnPlayerDied(PlayerRef deadPlayer, MarkForDeathData markForDeath)
		{
			if (deadPlayer == Player && _markForDeathToKillWerewolf.Contains(markForDeath) && !_werewolfToKill.IsNone)
			{
				_gameHistoryManager.AddEntry(_gaveTetanusGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "KnightWithRustySwordPlayer",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												},
												new()
												{
													Name = "WerewolfToKill",
													Data = _networkDataManager.PlayerInfos[_werewolfToKill].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												},
												new()
												{
													Name = "RoleName",
													Data = _gameManager.PlayerGameInfos[_werewolfToKill].Role.ID.HashCode.ToString(),
													Type = GameHistorySaveEntryVariableType.RoleName
												}
											});

				_killWerewolf = true;
			}
		}

		private void OnGameplayLoopStepStarts(GameplayLoopStep gameplayLoopStep)
		{
			if (gameplayLoopStep == GameplayLoopStep.DayTransition && !_werewolfToKill.IsNone && _killWerewolf)
			{
				_gameManager.AddMarkForDeath(_werewolfToKill, _markForDeathAddedByStab, 0);

				_werewolfToKill = PlayerRef.None;
				_killWerewolf = false;
			}
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.MarkForDeathAdded -= OnMarkForDeathAdded;
			_gameManager.Unsubscribe(this);
			_gameManager.GameplayLoopStepStarts -= OnGameplayLoopStepStarts;
		}
	}
}
