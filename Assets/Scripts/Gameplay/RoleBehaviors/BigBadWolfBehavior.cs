using Assets.Scripts.Data.Tags;
using Fusion;
using System.Collections;
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
		private float _chooseVillagerMaximumDuration = 10.0f;

		[SerializeField]
		private GameplayTag _choseVillagerGameHistoryEntry;

		[SerializeField]
		private GameplayTag _markForDeath;

		[SerializeField]
		private float _selectedVillagerHighlightDuration = 3.0f;

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
				Debug.LogError("Big Bad Wolf must have two night priorities: the first one to vote with the werewolves and the second one to kill a villager");
			}
		}

		private void OnPlayerDeathRevealEnded(PlayerRef deadPlayer)
		{
			if (_gameManager.IsPlayerInPlayerGroups(deadPlayer, _werewolvesPlayerGroups))
			{
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
			var villagers = _gameManager.GetPlayersFromGroups(_villagersPlayerGroups);

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
			_gameManager.RPC_DisplayTitle(Player, _noVillagersImage.CompactTagId);
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

			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, selectedVillager, true);
			_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(selectedVillager, true);
			_gameManager.HideUI();
#endif
			StartCoroutine(WaitToRemoveHighlight(selectedVillager));
		}

		private IEnumerator WaitToRemoveHighlight(PlayerRef selectedVillager)
		{
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
			_gameManager.RPC_DisplayTitle(Player, _lostPowerImage.CompactTagId);
			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void OnRoleCallDisconnected()
		{
			_gameManager.PlayerDeathRevealEnded -= OnPlayerDeathRevealEnded;

			StopAllCoroutines();
		}
	}
}
