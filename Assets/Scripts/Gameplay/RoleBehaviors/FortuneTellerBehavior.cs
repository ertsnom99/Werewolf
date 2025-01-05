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
	public class FortuneTellerBehavior : RoleBehavior
	{
		[Header("Choose Player")]
		[SerializeField]
		private GameplayTag _choosePlayerImage;

		[SerializeField]
		private float _choosePlayerMaximumDuration = 10.0f;

		[SerializeField]
		private GameplayTag _lookedPlayerRoleGameHistoryEntry;

		private IEnumerator _endRoleCallAfterTimeCoroutine;

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
			List<PlayerRef> choices = _gameManager.GetAlivePlayers();
			choices.Remove(Player);

			if (!_gameManager.SelectPlayers(Player,
											choices,
											_choosePlayerImage.CompactTagId,
											_choosePlayerMaximumDuration * _gameManager.GameSpeedModifier,
											false,
											1,
											ChoicePurpose.Other,
											OnPlayerSelected))
			{
				StartCoroutine(WaitToStopWaitingForPlayer());
			}

			_endRoleCallAfterTimeCoroutine = EndRoleCallAfterTime();
			StartCoroutine(_endRoleCallAfterTimeCoroutine);

			return isWakingUp = true;
		}

		private IEnumerator WaitToStopWaitingForPlayer()
		{
			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnPlayerSelected(PlayerRef[] players)
		{
			StopCoroutine(_endRoleCallAfterTimeCoroutine);

			if (players == null || players.Length <= 0 || players[0].IsNone || !_gameManager.RevealPlayerRole(players[0], Player, false, true, OnRoleRevealed))
			{
				_gameManager.StopWaintingForPlayer(Player);
				return;
			}

			_gameManager.RPC_HideUI(Player);

			PlayerRef playerLookedAt = players[0];

			_gameHistoryManager.AddEntry(_lookedPlayerRoleGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "FortuneTellerPlayer",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "PlayerLookedAt",
												Data = _networkDataManager.PlayerInfos[playerLookedAt].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "RoleName",
												Data = _gameManager.PlayerGameInfos[playerLookedAt].Role.GameplayTag.name,
												Type = GameHistorySaveEntryVariableType.RoleName
											}
										});
		}

		private void OnRoleRevealed(PlayerRef revealTo)
		{
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

		public override void OnPlayerChanged() { }
		
		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}
	}
}