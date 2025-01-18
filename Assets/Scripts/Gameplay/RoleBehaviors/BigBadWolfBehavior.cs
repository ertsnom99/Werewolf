using Assets.Scripts.Data.Tags;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class BigBadWolfBehavior : WerewolfBehavior
	{
		[Header("Big Bad Wolf")]
		[SerializeField]
		private GameplayTag[] _werewolvesPlayerGroups;

		[SerializeField]
		private GameplayTag _lostPowerGameHistoryEntry;

		[SerializeField]
		private GameplayTag[] _villagersPlayerGroups;

		[SerializeField]
		private GameplayTag _noVillagersImage;

		[SerializeField]
		private GameplayTag _chooseVillagerImage;

		[SerializeField]
		private float _chooseVillagerMaximumDuration;

		[SerializeField]
		private GameplayTag _choseVillagerGameHistoryEntry;

		[SerializeField]
		private GameplayTag _markForDeath;

		[SerializeField]
		private float _selectedVillagerHighlightDuration;

		[SerializeField]
		private GameplayTag _lostPowerImage;

		private bool _hasPower = true;

		private IEnumerator _endRoleCallAfterTimeCoroutine;

		public override void Initialize()
		{
			base.Initialize();

			_gameManager.PlayerDeathRevealEnded += OnPlayerDeathRevealEnded;

			if (NightPriorities.Count < 2)
			{
				Debug.LogError($"{nameof(BigBadWolfBehavior)} must have two night priorities: the first one to vote with the werewolves and the second one to kill a villager");
			}
		}

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			if (priorityIndex == NightPriorities[0].index)
			{
				VoteForVillagers();
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
			List<PlayerRef> villagers = _gameManager.GetPlayersFromPlayerGroups(_villagersPlayerGroups).ToList();

			if (villagers.Count <= 0)
			{
				StartCoroutine(ShowNoVillagers());
				return false;
			}

			if (!_gameManager.SelectPlayers(Player,
											villagers,
											_chooseVillagerImage.CompactTagId,
											_chooseVillagerMaximumDuration * _gameManager.GameSpeedModifier,
											false,
											1,
											ChoicePurpose.Kill,
											OnVillagerSelected))
			{
				StartCoroutine(WaitToStopWaitingForPlayer());
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
				_gameManager.RPC_DisplayTitle(Player, _noVillagersImage.CompactTagId);
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

			_gameHistoryManager.AddEntry(_choseVillagerGameHistoryEntry,
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
													Data = _gameManager.PlayerGameInfos[selectedVillager].Role.GameplayTag.name,
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

			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, selectedVillager, false);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(selectedVillager, false);
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
				_gameManager.RPC_DisplayTitle(Player, _lostPowerImage.CompactTagId);
			}

			yield return 0;

			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnPlayerDeathRevealEnded(PlayerRef deadPlayer, GameplayTag markForDeath)
		{
			if (Player.IsNone
				|| Player == deadPlayer
				|| !_gameManager.PlayerGameInfos[Player].IsAlive
				|| !_gameManager.IsPlayerInPlayerGroups(deadPlayer, _werewolvesPlayerGroups))
			{
				return;
			}

			_hasPower = false;

			_gameHistoryManager.AddEntry(_lostPowerGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "BigBadWolfPlayer",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												}
										});

			_gameManager.PlayerDeathRevealEnded -= OnPlayerDeathRevealEnded;
		}

		public override void OnPlayerChanged() { }

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			_gameManager.PlayerDeathRevealEnded -= OnPlayerDeathRevealEnded;
		}
	}
}
