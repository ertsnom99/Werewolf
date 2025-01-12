using Assets.Scripts.Data.Tags;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Gameplay.Role
{
	public class SaviourBehavior : RoleBehavior
	{
		[Header("Save Player")]
		[SerializeField]
		private GameplayTag _choosePlayerImage;

		[SerializeField]
		private float _choosePlayerMaximumDuration = 10.0f;

		[SerializeField]
		private GameplayTag _chosePlayerToProtectGameHistoryEntry;

		[SerializeField]
		private float _playerHighlightHoldDuration = 3.0f;

		[SerializeField]
		private GameplayTag[] _marksForDeathRemovedByProtection;

		private IEnumerator _endRoleCallAfterTimeCoroutine;

		private int _lastSelectionNightCount;
		private PlayerRef _selectedPlayer;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_gameManager.MarkForDeathAdded += OnMarkForDeathAdded;
		}

		public override void OnSelectedToDistribute(List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
		{
			if (_lastSelectionNightCount + 1 < nightCount)
			{
				_selectedPlayer = PlayerRef.None;
			}

			_lastSelectionNightCount = nightCount;

			List<PlayerRef> choices = _gameManager.GetAlivePlayers();

			if (!_selectedPlayer.IsNone)
			{
				choices.Remove(_selectedPlayer);
				_selectedPlayer = PlayerRef.None;
			}

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
			_selectedPlayer = PlayerRef.None;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnPlayerSelected(PlayerRef[] players)
		{
			StopCoroutine(_endRoleCallAfterTimeCoroutine);

			if (players == null || players.Length <= 0 || players[0].IsNone)
			{
				_selectedPlayer = PlayerRef.None;
				_gameManager.StopWaintingForPlayer(Player);
				return;
			}

			_selectedPlayer = players[0];

			_gameHistoryManager.AddEntry(_chosePlayerToProtectGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "SaviorPlayer",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											},
											new()
											{
												Name = "ProtectedPlayer",
												Data = _networkDataManager.PlayerInfos[_selectedPlayer].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			StartCoroutine(HighlightSelectedPlayer());
		}

		private IEnumerator HighlightSelectedPlayer()
		{
			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_HideUI(Player);
				_gameManager.RPC_SetPlayerCardHighlightVisible(Player, _selectedPlayer, true);
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.HideUI();
			_gameManager.SetPlayerCardHighlightVisible(_selectedPlayer, true);
#endif
			yield return new WaitForSeconds(_playerHighlightHoldDuration * _gameManager.GameSpeedModifier);

			_gameManager.RPC_SetPlayerCardHighlightVisible(Player, _selectedPlayer, false);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayerCardHighlightVisible(_selectedPlayer, false);
#endif
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

		private void OnMarkForDeathAdded(PlayerRef player, GameplayTag markForDeath)
		{
			if (player == _selectedPlayer && _marksForDeathRemovedByProtection.Contains(markForDeath))
			{
				_gameManager.RemoveMarkForDeath(player, markForDeath);
			}
		}

		public override void OnPlayerChanged()
		{
			_selectedPlayer = PlayerRef.None;
		}

		public override void OnRoleCallDisconnected()
		{
			StopAllCoroutines();
		}

		private void OnDestroy()
		{
			_gameManager.MarkForDeathAdded -= OnMarkForDeathAdded;
		}
	}
}