using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Data.Tags;
using Fusion;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;
using static Werewolf.Managers.GameManager;

namespace Werewolf.Gameplay.Role
{
	public class KnightWithRustySwordBehavior : RoleBehavior
	{
		[Header("Kill Werewolf")]
		[SerializeField]
		private GameplayTag[] _markForDeathToKillWerewolf;

		[SerializeField]
		private GameplayTag[] _werewolvesPlayerGroups;

		[SerializeField]
		private GameplayTag _gaveTetanusGameHistoryEntry;

		[SerializeField]
		private GameplayTag _markForDeathAddedByStab;

		private PlayerRef _werewolfToKill;
		private bool _killWerewolf;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_gameManager.MarkForDeathAdded += OnMarkForDeathAdded;
			_gameManager.PlayerDeathRevealEnded += OnPlayerDeathRevealEnded;
			_gameManager.GameplayLoopStepStarts += OnGameplayLoopStepStarts;
		}

		public override void OnSelectedToDistribute(List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			return isWakingUp = false;
		}

		private void OnMarkForDeathAdded(PlayerRef player, GameplayTag markForDeath)
		{
			if (player == Player && _markForDeathToKillWerewolf.Contains(markForDeath))
			{
				_werewolfToKill = _gameManager.FindNextPlayer(Player, searchToLeft: true, mustBeAwake: true, _werewolvesPlayerGroups);
			}
		}

		private void OnPlayerDeathRevealEnded(PlayerRef deadPlayer, GameplayTag markForDeath)
		{
			if (deadPlayer == Player && _markForDeathToKillWerewolf.Contains(markForDeath) && _werewolfToKill != PlayerRef.None)
			{
				_gameHistoryManager.AddEntry(_gaveTetanusGameHistoryEntry,
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
													Data = _gameManager.PlayerGameInfos[_werewolfToKill].Role.GameplayTag.name,
													Type = GameHistorySaveEntryVariableType.RoleName
												}
											});

				_killWerewolf = true;
			}
		}

		private void OnGameplayLoopStepStarts(GameplayLoopStep gameplayLoopStep)
		{
			if (gameplayLoopStep == GameplayLoopStep.DayTransition && _werewolfToKill != PlayerRef.None && _killWerewolf)
			{
				_gameManager.AddMarkForDeath(_werewolfToKill, _markForDeathAddedByStab, 0);

				_werewolfToKill = PlayerRef.None;
				_killWerewolf = false;
			}
		}

		public override void ReInitialize() { }

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.MarkForDeathAdded -= OnMarkForDeathAdded;
			_gameManager.PlayerDeathRevealEnded -= OnPlayerDeathRevealEnded;
			_gameManager.GameplayLoopStepStarts -= OnGameplayLoopStepStarts;
		}
	}
}
