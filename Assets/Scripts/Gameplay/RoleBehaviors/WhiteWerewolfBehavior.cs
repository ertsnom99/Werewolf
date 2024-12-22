using Assets.Scripts.Data.Tags;
using Fusion;
using System.Collections;
using UnityEngine;
using static Werewolf.GameHistoryManager;

namespace Werewolf
{
	public class WhiteWerewolfBehavior : WerewolfBehavior
	{
		[Header("Werewolf Choice")]
		[SerializeField]
		private GameplayTag[] _otherWerewolvesPlayerGroups;

		[SerializeField]
		private GameplayTag _noOtherWerewolvesImage;

		[SerializeField]
		private GameplayTag _chooseOtherWerewolfImage;

		[SerializeField]
		private float _chooseOtherWerewolfMaximumDuration = 10.0f;

		[SerializeField]
		private GameplayTag _votedWerewolfGameHistoryEntry;

		[SerializeField]
		private GameplayTag _markForDeath ;

		[SerializeField]
		private float _selectedWerewolfHighlightDuration = 3.0f;

		private IEnumerator _endRoleCallAfterTimeCoroutine;

		public override void Initialize()
		{
			base.Initialize();	

			if (NightPriorities.Count < 2)
			{
				Debug.LogError("White werewolf must have two night priorities: the first one to vote with the werewolves and the second one to vote against the werewolves");
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
				isWakingUp = ChooseOtherWerewolf();
				return true;
			}

			return isWakingUp = false;
		}

		private bool ChooseOtherWerewolf()
		{
			var werewolves = _gameManager.GetPlayersFromGroup(_otherWerewolvesPlayerGroups);

			if (werewolves.Count <= 0) 
			{
				StartCoroutine(ShowNoOtherWerewolf());
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

		private IEnumerator ShowNoOtherWerewolf()
		{
			_gameManager.RPC_DisplayTitle(Player, _noOtherWerewolvesImage.CompactTagId);
			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator WaitToStopWaitingForPlayer()
		{
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

			_gameHistoryManager.AddEntry(_votedWerewolfGameHistoryEntry,
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

			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, selectedWerewolf, true);
			_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(selectedWerewolf, true);
			_gameManager.HideUI();
#endif
			StartCoroutine(WaitToRemoveHighlight(selectedWerewolf));
		}

		private IEnumerator WaitToRemoveHighlight(PlayerRef selectedWerewolf)
		{
			yield return new WaitForSeconds(_selectedWerewolfHighlightDuration * _gameManager.GameSpeedModifier);

			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, selectedWerewolf, false);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(selectedWerewolf, false);
#endif
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
