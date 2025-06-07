using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utilities.GameplayData;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class FoxBehavior : RoleBehavior
	{
		[Header("Find Werewolves")]
		[SerializeField]
		private TitleScreenData _lostPowerTitleScreen;

		[SerializeField]
		private TitleScreenData _choosePlayerTitleScreen;

		[SerializeField]
		private float _choosePlayerMaximumDuration;

		[SerializeField]
		private PlayerGroupData[] _werewolvesPlayerGroups;

		[SerializeField]
		private GameHistoryEntryData _sniffedWerewolfGameHistoryEntry;

		[SerializeField]
		private TitleScreenData _foundWerewolfTitleScreen;

		[SerializeField]
		private GameHistoryEntryData _lostPowerGameHistoryEntry;

		[SerializeField]
		private TitleScreenData _foundNoWerewolfTitleScreen;

		[SerializeField]
		private float _resultTitleHoldDuration;

		private UniqueID[] _werewolvesPlayerGroupIDs;
		private bool _hasPower = true;
		private IEnumerator _endRoleCallAfterTimeCoroutine;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			_werewolvesPlayerGroupIDs = GameplayData.GetIDs(_werewolvesPlayerGroups);

			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int priorityIndex, out bool isWakingUp)
		{
			if (!_hasPower)
			{
				StartCoroutine(ShowLostPower());

				isWakingUp = false;
				return true;
			}

			if (!_gameManager.SelectPlayers(Player,
											_gameManager.GetAlivePlayers(),
											_choosePlayerTitleScreen.ID.HashCode,
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
			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_DisplayTitle(Player, _lostPowerTitleScreen.ID.HashCode);
			}

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

			StartCoroutine(CheckForWerewolves(players[0]));
		}

		private IEnumerator CheckForWerewolves(PlayerRef middlePlayer)
		{
			HashSet<PlayerRef> playersToCheck = _gameManager.FindSurroundingPlayers(middlePlayer);
			playersToCheck.Add(middlePlayer);

			bool werewolfFound = false;

			foreach (PlayerRef player in playersToCheck)
			{
				if (_gameManager.IsPlayerInPlayerGroups(player, _werewolvesPlayerGroupIDs))
				{
					werewolfFound = true;
				}

				if (_networkDataManager.PlayerInfos[Player].IsConnected)
				{
					_gameManager.RPC_SetPlayerCardHighlightVisible(Player, player, true);
				}
#if UNITY_SERVER && UNITY_EDITOR
				_gameManager.SetPlayerCardHighlightVisible(player, true);
#endif
			}

			_gameHistoryManager.AddEntry(werewolfFound ? _sniffedWerewolfGameHistoryEntry.ID : _lostPowerGameHistoryEntry.ID,
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
												Data = ConcatenatePlayersNickname(playersToCheck.ToArray(), _networkDataManager),
												Type = GameHistorySaveEntryVariableType.Players
											}
										});

			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_HideUI(Player);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(_gameManager.GameConfig.UITransitionNormalDuration);

			if (werewolfFound)
			{
				_gameManager.RPC_DisplayTitle(Player, _foundWerewolfTitleScreen.ID.HashCode);
			}
			else
			{
				_gameManager.RPC_DisplayTitle(Player, _foundNoWerewolfTitleScreen.ID.HashCode);
				_hasPower = false;
			}

			yield return new WaitForSeconds(_resultTitleHoldDuration * _gameManager.GameSpeedModifier);

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

		public override void OnPlayerChanged()
		{
			_hasPower = true;
		}

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}
	}
}