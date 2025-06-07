using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utilities.GameplayData;
using Werewolf.Data;
using Werewolf.Managers;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class BigBadWolfBehavior : WerewolfBehavior, IGameManagerSubscriber
	{
		[Header("Big Bad Wolf")]
		[SerializeField]
		private GameHistoryEntryData _lostPowerGameHistoryEntry;

		[SerializeField]
		private PlayerGroupData _villagersPlayerGroup;

		[SerializeField]
		private TitleScreenData _noVillagersTitleScreen;

		[SerializeField]
		private TitleScreenData _chooseVillagerTitleScreen;

		[SerializeField]
		private float _chooseVillagerMaximumDuration;

		[SerializeField]
		private GameHistoryEntryData _choseVillagerGameHistoryEntry;

		[SerializeField]
		private MarkForDeathData _markForDeath;

		[SerializeField]
		private float _selectedVillagerHighlightDuration;

		[SerializeField]
		private TitleScreenData _lostPowerTitleScreen;

		[SerializeField]
		private PlayerGroupData[] _werewolvesPlayerGroups;

		private UniqueID[] _werewolvesPlayerGroupIDs;
		private bool _hasPower = true;
		private IEnumerator _endRoleCallAfterTimeCoroutine;
		private bool _revealedPlayerIsWerewolf;

		public override void Initialize()
		{
			base.Initialize();

			_werewolvesPlayerGroupIDs = GameplayData.GetIDs(_werewolvesPlayerGroups);

			_gameManager.WaitBeforeFlipDeadPlayerRoleEnded += OnWaitBeforeFlipDeadPlayerRoleEnded;
			_gameManager.Subscribe(this);
			_gameManager.DeathRevealEnded += OnDeathRevealEnded;

			if (NightPriorities.Count < 2)
			{
				Debug.LogError($"{nameof(BigBadWolfBehavior)} must have two night priorities: the first one to vote with the werewolves and the second one to kill a villager");
			}
		}

		public override bool OnRoleCall(int priorityIndex, out bool isWakingUp)
		{
			if (priorityIndex == NightPriorities[0].index)
			{
				VoteForVillager();
				return isWakingUp = true;
			}
			else if (priorityIndex == NightPriorities[1].index)
			{
				if (_hasPower)
				{
					isWakingUp = KillVillager();
				}
				else
				{
					StartCoroutine(ShowLostPower());
					isWakingUp = false;
				}

				return true;
			}

			return isWakingUp = false;
		}

		private bool KillVillager()
		{
			List<PlayerRef> villagers = _gameManager.GetPlayersFromPlayerGroup(_villagersPlayerGroup.ID).ToList();

			if (villagers.Count <= 0)
			{
				StartCoroutine(ShowNoVillagers());
				return false;
			}

			if (!_gameManager.SelectPlayers(Player,
											villagers,
											_chooseVillagerTitleScreen.ID.HashCode,
											_chooseVillagerMaximumDuration * _gameManager.GameSpeedModifier,
											false,
											1,
											ChoicePurpose.Kill,
											OnVillagerSelected))
			{
				if (villagers.Count <= 0)
				{
					StartCoroutine(ShowNoVillagers());
				}
				else
				{
					StartCoroutine(WaitToStopWaitingForPlayer());
				}

				return false;
			}

			_endRoleCallAfterTimeCoroutine = EndRoleCallAfterTime();
			StartCoroutine(_endRoleCallAfterTimeCoroutine);

			return true;
		}

		private IEnumerator ShowNoVillagers()
		{
			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_DisplayTitle(Player, _noVillagersTitleScreen.ID.HashCode);
			}

			yield return 0;
			
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnVillagerSelected(PlayerRef[] players)
		{
			StopCoroutine(_endRoleCallAfterTimeCoroutine);

			if (players == null || players.Length <= 0 || players[0].IsNone)
			{
				_gameManager.StopWaintingForPlayer(Player);
				return;
			}

			var selectedVillager = players[0];

			_gameHistoryManager.AddEntry(_choseVillagerGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "BigBadWolfPlayer",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												},
												new()
												{
													Name = "SelectedPlayer",
													Data = _networkDataManager.PlayerInfos[selectedVillager].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												},
												new()
												{
													Name = "RoleName",
													Data = _gameManager.PlayerGameInfos[selectedVillager].Role.ID.HashCode.ToString(),
													Type = GameHistorySaveEntryVariableType.RoleName
												}
											});

			_gameManager.AddMarkForDeath(players[0], _markForDeath);
			StartCoroutine(HighlightSelectedVillager(selectedVillager));
		}

		private IEnumerator HighlightSelectedVillager(PlayerRef selectedVillager)
		{
			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_HideUI(Player);
				_gameManager.RPC_SetPlayerCardHighlightVisible(Player, selectedVillager, true);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.HideUI();
			_gameManager.SetPlayerCardHighlightVisible(selectedVillager, true);
#endif
			yield return new WaitForSeconds(_selectedVillagerHighlightDuration * _gameManager.GameSpeedModifier);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator WaitToStopWaitingForPlayer()
		{
			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator EndRoleCallAfterTime()
		{
			float timeLeft = _chooseVillagerMaximumDuration * _gameManager.GameSpeedModifier;

			while (timeLeft > 0)
			{
				yield return 0;
				timeLeft -= Time.deltaTime;
			}

			_gameManager.StopSelectingPlayers(Player);
			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator ShowLostPower()
		{
			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_DisplayTitle(Player, _lostPowerTitleScreen.ID.HashCode);
			}

			yield return 0;

			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnWaitBeforeFlipDeadPlayerRoleEnded(PlayerRef deadPlayer)
		{
			if (Player.IsNone
				|| Player == deadPlayer
				|| !_gameManager.PlayerGameInfos[Player].IsAlive)
			{
				return;
			}

			_revealedPlayerIsWerewolf = _gameManager.IsPlayerInPlayerGroups(deadPlayer, _werewolvesPlayerGroupIDs);
		}

		void IGameManagerSubscriber.OnPlayerDied(PlayerRef deadPlayer, MarkForDeathData markForDeath)
		{
			if (Player.IsNone
				|| Player == deadPlayer
				|| !_gameManager.PlayerGameInfos[Player].IsAlive
				|| !_revealedPlayerIsWerewolf)
			{
				return;
			}

			_hasPower = false;

			_gameHistoryManager.AddEntry(_lostPowerGameHistoryEntry.ID,
										new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "BigBadWolfPlayer",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												}
										});

			_gameManager.WaitBeforeFlipDeadPlayerRoleEnded -= OnWaitBeforeFlipDeadPlayerRoleEnded;
		}

		private void OnDeathRevealEnded()
		{
			if (_hasPower)
			{
				return;
			}

			_gameManager.Unsubscribe(this);
			_gameManager.DeathRevealEnded -= OnDeathRevealEnded;
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			_gameManager.WaitBeforeFlipDeadPlayerRoleEnded -= OnWaitBeforeFlipDeadPlayerRoleEnded;
			_gameManager.Unsubscribe(this);
			_gameManager.DeathRevealEnded -= OnDeathRevealEnded;
		}
	}
}
