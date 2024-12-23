using Assets.Scripts.Data.Tags;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class FoxBehavior : RoleBehavior
	{
		[Header("Find Werewolfs")]
		[SerializeField]
		private GameplayTag _lostPowerImage;

		[SerializeField]
		private GameplayTag _choosePlayerImage;

		[SerializeField]
		private float _choosePlayerMaximumDuration = 10.0f;

		[SerializeField]
		private GameplayTag[] _werewolfPlayerGroups;

		[SerializeField]
		private GameplayTag _sniffedWerewolfGameHistoryEntry;

		[SerializeField]
		private GameplayTag _foundWerewolfImage;

		[SerializeField]
		private GameplayTag _lostPowerGameHistoryEntry;

		[SerializeField]
		private GameplayTag _foundNoWerewolfImage;

		[SerializeField]
		private float _resultTitleHoldDuration = 3.0f;

		private IEnumerator _endRoleCallAfterTimeCoroutine;

		private bool _hasPower = true;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;
		}

		public override void OnSelectedToDistribute(List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			if (!_hasPower)
			{
				StartCoroutine(ShowLostPower());

				isWakingUp = false;
				return true;
			}

			if (!_gameManager.SelectPlayers(Player,
											_gameManager.GetAlivePlayers(),
											_choosePlayerImage.CompactTagId,
											_choosePlayerMaximumDuration * _gameManager.GameSpeedModifier,
											false,
											1,
											ChoicePurpose.Other,
											OnPlayerSelected))
			{
				StartCoroutine(WaitToStopWaitingForPlayer());
				return isWakingUp = false;
			}

			_endRoleCallAfterTimeCoroutine = EndRoleCallAfterTime();
			StartCoroutine(_endRoleCallAfterTimeCoroutine);

			return isWakingUp = true;
		}

		private IEnumerator ShowLostPower()
		{
			_gameManager.RPC_DisplayTitle(Player, _lostPowerImage.CompactTagId);
			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator WaitToStopWaitingForPlayer()
		{
			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnPlayerSelected(PlayerRef[] players)
		{
			StopCoroutine(_endRoleCallAfterTimeCoroutine);

			if (players == null || players.Length <= 0 || players[0].IsNone)
			{
				_gameManager.StopWaintingForPlayer(Player);
				return;
			}

			StartCoroutine(CheckForWerewolfs(players[0]));
		}

		private IEnumerator CheckForWerewolfs(PlayerRef middlePlayer)
		{
			List<PlayerRef> playersToCheck = _gameManager.FindSurroundingPlayers(middlePlayer);
			playersToCheck.Add(middlePlayer);

			bool werewolfFound = false;

			foreach (PlayerRef player in playersToCheck)
			{
				if (_gameManager.IsPlayerInPlayerGroups(player, _werewolfPlayerGroups))
				{
					werewolfFound = true;
				}

				_gameManager.RPC_SetPlayerCardHighlightVisible(Player, player, true);
			}

			_gameHistoryManager.AddEntry(werewolfFound ? _sniffedWerewolfGameHistoryEntry : _lostPowerGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "FoxPlayer",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "SniffedPlayers",
												Data = ConcatenatePlayersNickname(playersToCheck, _networkDataManager),
												Type = GameHistorySaveEntryVariableType.Players
											}
										});

			_gameManager.RPC_HideUI(Player);

			yield return new WaitForSeconds(_gameManager.Config.UITransitionNormalDuration);

			if (werewolfFound)
			{
				_gameManager.RPC_DisplayTitle(Player, _foundWerewolfImage.CompactTagId);
			}
			else
			{
				_gameManager.RPC_DisplayTitle(Player, _foundNoWerewolfImage.CompactTagId);
				_hasPower = false;
			}

			yield return new WaitForSeconds(_resultTitleHoldDuration * _gameManager.GameSpeedModifier);

			foreach (PlayerRef player in playersToCheck)
			{
				_gameManager.RPC_SetPlayerCardHighlightVisible(Player, player, false);
			}

			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator EndRoleCallAfterTime()
		{
			float timeLeft = _choosePlayerMaximumDuration * _gameManager.GameSpeedModifier;

			while (timeLeft > 0)
			{
				yield return 0;
				timeLeft -= Time.deltaTime;
			}

			_gameManager.StopSelectingPlayers(Player);
			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void ReInitialize()
		{
			_hasPower = true;
		}

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}
	}
}