using Assets.Scripts.Data.Tags;
using Fusion;
using System.Collections;
using UnityEngine;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class WhiteWerewolfBehavior : WerewolfBehavior
	{
		[Header("White Werewolf")]
		[SerializeField]
		private GameplayTag[] _otherWerewolvesPlayerGroups;

		[SerializeField]
		private GameplayTag _noOtherWerewolvesImage;

		[SerializeField]
		private GameplayTag _chooseOtherWerewolfImage;

		[SerializeField]
		private float _chooseOtherWerewolfMaximumDuration = 10.0f;

		[SerializeField]
		private GameplayTag _choseWerewolfGameHistoryEntry;

		[SerializeField]
		private GameplayTag _markForDeath;

		[SerializeField]
		private float _selectedWerewolfHighlightDuration = 3.0f;

		private IEnumerator _endRoleCallAfterTimeCoroutine;

		public override void Initialize()
		{
			base.Initialize();	

			if (NightPriorities.Count < 2)
			{
				Debug.LogError("White Werewolf must have two night priorities: the first one to vote with the werewolves and the second one to kill a werewolf");
			}
		}

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			if (priorityIndex == NightPriorities[0].index)
			{
				VoteForVillagers();
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
			var werewolves = _gameManager.GetPlayersFromGroups(_otherWerewolvesPlayerGroups);

			if (werewolves.Count <= 0) 
			{
				StartCoroutine(ShowNoOtherWerewolves());
				return false;
			}

			if (!_gameManager.SelectPlayers(Player,
											werewolves,
											_chooseOtherWerewolfImage.CompactTagId,
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
			_gameManager.RPC_DisplayTitle(Player, _noOtherWerewolvesImage.CompactTagId);
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

			_gameHistoryManager.AddEntry(_choseWerewolfGameHistoryEntry,
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
													Data = _gameManager.PlayerGameInfos[selectedWerewolf].Role.GameplayTag.name,
													Type = GameHistorySaveEntryVariableType.RoleName
												}
											});

			_gameManager.AddMarkForDeath(players[0], _markForDeath);
			StartCoroutine(HighlightSelectedWerewolf(selectedWerewolf));
		}

		private IEnumerator HighlightSelectedWerewolf(PlayerRef selectedWerewolf)
		{
			_gameManager.RPC_HideUI(Player);
			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, selectedWerewolf, true);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.HideUI();
			_gameManager.SetPlayerCardHighlightVisible(selectedWerewolf, true);
#endif
			yield return new WaitForSeconds(_selectedWerewolfHighlightDuration * _gameManager.GameSpeedModifier);
			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, selectedWerewolf, false);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(selectedWerewolf, false);
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
