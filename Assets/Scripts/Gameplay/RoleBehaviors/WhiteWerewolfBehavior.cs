using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utilities.GameplayData;
using Werewolf.Data;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class WhiteWerewolfBehavior : WerewolfBehavior
	{
		[Header("White Werewolf")]
		[SerializeField]
		private PlayerGroupData[] _otherWerewolvesPlayerGroups;

		[SerializeField]
		private TitleScreenData _noOtherWerewolvesTitleScreen;

		[SerializeField]
		private TitleScreenData _chooseOtherWerewolfTitleScreen;

		[SerializeField]
		private float _chooseOtherWerewolfMaximumDuration;

		[SerializeField]
		private GameHistoryEntryData _choseWerewolfGameHistoryEntry;

		[SerializeField]
		private MarkForDeathData _markForDeath;

		[SerializeField]
		private float _selectedWerewolfHighlightDuration;

		private UniqueID[] _otherWerewolvesPlayerGroupIDs;
		private IEnumerator _endRoleCallAfterTimeCoroutine;

		public override void Initialize()
		{
			base.Initialize();

			_otherWerewolvesPlayerGroupIDs = GameplayData.GetIDs(_otherWerewolvesPlayerGroups);

			if (NightPriorities.Count < 2)
			{
				Debug.LogError($"{nameof(WhiteWerewolfBehavior)} must have two night priorities: the first one to vote with the werewolves and the second one to kill a werewolf");
			}
		}

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			if (priorityIndex == NightPriorities[0].index)
			{
				VoteForVillager();
				return isWakingUp = true;
			}
			else if (priorityIndex == NightPriorities[1].index && nightCount % 2 == 0)
			{
				isWakingUp = KillWerewolf();
				return true;
			}

			return isWakingUp = false;
		}

		private bool KillWerewolf()
		{
			List<PlayerRef> werewolves = _gameManager.GetPlayersFromPlayerGroups(_otherWerewolvesPlayerGroupIDs).ToList();

			if (werewolves.Count <= 0) 
			{
				StartCoroutine(ShowNoOtherWerewolves());
				return false;
			}

			if (!_gameManager.SelectPlayers(Player,
											werewolves,
											_chooseOtherWerewolfTitleScreen.ID.HashCode,
											_chooseOtherWerewolfMaximumDuration * _gameManager.GameSpeedModifier,
											false,
											1,
											ChoicePurpose.Kill,
											OnOtherWerewolfSelected))
			{
				StartCoroutine(WaitToStopWaitingForPlayer());
				return false;
			}

			_endRoleCallAfterTimeCoroutine = EndRoleCallAfterTime();
			StartCoroutine(_endRoleCallAfterTimeCoroutine);

			return true;
		}

		private IEnumerator ShowNoOtherWerewolves()
		{
			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_DisplayTitle(Player, _noOtherWerewolvesTitleScreen.ID.HashCode);
			}

			yield return 0;
			
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnOtherWerewolfSelected(PlayerRef[] players)
		{
			StopCoroutine(_endRoleCallAfterTimeCoroutine);

			if (players == null || players.Length <= 0 || players[0].IsNone)
			{
				_gameManager.StopWaintingForPlayer(Player);
				return;
			}

			var selectedWerewolf = players[0];

			_gameHistoryManager.AddEntry(_choseWerewolfGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "WhiteWerewolfPlayer",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												},
												new()
												{
													Name = "SelectedPlayer",
													Data = _networkDataManager.PlayerInfos[selectedWerewolf].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												},
												new()
												{
													Name = "RoleName",
													Data = _gameManager.PlayerGameInfos[selectedWerewolf].Role.ID.HashCode.ToString(),
													Type = GameHistorySaveEntryVariableType.RoleName
												}
											});

			_gameManager.AddMarkForDeath(players[0], _markForDeath);
			StartCoroutine(HighlightSelectedWerewolf(selectedWerewolf));
		}

		private IEnumerator HighlightSelectedWerewolf(PlayerRef selectedWerewolf)
		{
			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_HideUI(Player);
				_gameManager.RPC_SetPlayerCardHighlightVisible(Player, selectedWerewolf, true);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.HideUI();
			_gameManager.SetPlayerCardHighlightVisible(selectedWerewolf, true);
#endif
			yield return new WaitForSeconds(_selectedWerewolfHighlightDuration * _gameManager.GameSpeedModifier);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator WaitToStopWaitingForPlayer()
		{
			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator EndRoleCallAfterTime()
		{
			float timeLeft = _chooseOtherWerewolfMaximumDuration * _gameManager.GameSpeedModifier;

			while (timeLeft > 0)
			{
				yield return 0;
				timeLeft -= Time.deltaTime;
			}

			_gameManager.StopSelectingPlayers(Player);
			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}
	}
}
