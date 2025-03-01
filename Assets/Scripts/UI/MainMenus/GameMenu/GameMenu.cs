using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using Werewolf.Data;
using Werewolf.Network;

namespace Werewolf.UI
{
	public class GameMenu : MonoBehaviour
	{
		[Header("UI")]
		[SerializeField]
		private Button _historyBtn;

		[SerializeField]
		private RoomMenu _roomMenu;

		[SerializeField]
		private SettingsMenu _settingsMenu;

		[SerializeField]
		private HistoryMenu _historyMenu;

		[SerializeField]
		private Button _startGameBtn;

		[SerializeField]
		private Button _leaveGameBtn;

		[Header("Warnings")]
		[SerializeField]
		private LocalizedString _notEnoughPlayers;

		[SerializeField]
		private LocalizedString _invalidRolesSetup;

		public event Action<PlayerRef> KickPlayerClicked;
		public event Action<PlayerRef, string> ChangeNicknameClicked;
		public event Action<int[], int[]> RolesSetupChanged;
		public event Action<GameSpeed> GameSpeedChanged;
		public event Action StartGameClicked;
		public event Action LeaveGameClicked;

		private GameConfig _gameConfig;
		private PlayerRef _localPlayer;

		private NetworkDataManager _networkDataManager;

		public void Initialize(NetworkDataManager networkDataManager, GameConfig gameConfig, PlayerRef localPlayer, string gameHistory)
		{
			_networkDataManager = networkDataManager;
			_gameConfig = gameConfig;
			_localPlayer = localPlayer;

			if (!_networkDataManager || _localPlayer == null)
			{
				return;
			}

			bool historyAvailable = !string.IsNullOrEmpty(gameHistory);
			_historyBtn.interactable = historyAvailable;

			_roomMenu.Initialize(networkDataManager, localPlayer, gameConfig.MinNicknameCharacterCount);
			_settingsMenu.Initialize(networkDataManager, gameConfig, localPlayer);

			if (historyAvailable)
			{
				_historyMenu.Initialize(gameHistory);
			}
			
			((IntVariable)_notEnoughPlayers["MinPlayerCount"]).Value = _gameConfig.MinPlayerCount;

			UpdateUI();

			_networkDataManager.PlayerInfosChanged += UpdateUI;
			_networkDataManager.RolesSetupChanged += UpdateUI;
			_networkDataManager.GameSetupReadyChanged += UpdateUI;
			_networkDataManager.InvalidRolesSetupReceived += OnInvalidRolesSetupReceived;
			_roomMenu.KickPlayerClicked += OnKickPlayerClicked;
			_roomMenu.ChangeNicknameClicked += OnChangeNicknameClicked;
			_settingsMenu.RolesSetupChanged += OnRolesSetupChanged;
			_settingsMenu.GameSpeedChanged += OnGameSpeedChanged;
		}

		private void UpdateUI()
		{
			List<LocalizedString> warnings = new();
			bool isRolesSetupValid = _networkDataManager.IsRolesSetupValid(warnings);
			bool localPlayerInfoExist = _networkDataManager.PlayerInfos.TryGet(_localPlayer, out NetworkPlayerInfo localPlayerInfo);
			bool isLocalPlayerLeader = localPlayerInfoExist && localPlayerInfo.IsLeader;
			bool enoughPlayers = _networkDataManager.PlayerInfos.Count >= _gameConfig.MinPlayerCount;

			if (!enoughPlayers)
			{
				warnings.Insert(0, _notEnoughPlayers);
			}

			_settingsMenu.UpdateWarnings(warnings);

			_startGameBtn.interactable = isLocalPlayerLeader
										&& _gameConfig.MinPlayerCount > -1
										&& enoughPlayers
										&& !_networkDataManager.GameSetupReady
										&& isRolesSetupValid;
			_leaveGameBtn.interactable = !_networkDataManager.GameSetupReady;
		}

		private void OnInvalidRolesSetupReceived()
		{
			_settingsMenu.UpdateWarnings(new() { _invalidRolesSetup });
		}

		private void OnKickPlayerClicked(PlayerRef player)
		{
			KickPlayerClicked?.Invoke(player);
		}

		private void OnChangeNicknameClicked(PlayerRef player, string nickname)
		{
			ChangeNicknameClicked?.Invoke(player, nickname);
		}

		private void OnRolesSetupChanged(int[] mandatoryRoleIDs, int[] optionalRoleIDs)
		{
			RolesSetupChanged?.Invoke(mandatoryRoleIDs, optionalRoleIDs);
		}

		private void OnGameSpeedChanged(GameSpeed gameSpeed)
		{
			GameSpeedChanged?.Invoke(gameSpeed);
		}

		public void OnShowRoom()
		{
			_roomMenu.gameObject.SetActive(true);
			_settingsMenu.gameObject.SetActive(false);
			_historyMenu.gameObject.SetActive(false);
		}

		public void OnShowSettings()
		{
			_roomMenu.gameObject.SetActive(false);
			_settingsMenu.gameObject.SetActive(true);
			_historyMenu.gameObject.SetActive(false);
		}

		public void OnShowHistory()
		{
			_roomMenu.gameObject.SetActive(false);
			_settingsMenu.gameObject.SetActive(false);
			_historyMenu.gameObject.SetActive(true);
		}

		public void OnStartGame()
		{
			StartGameClicked?.Invoke();
		}

		public void OnLeaveGame()
		{
			LeaveGameClicked?.Invoke();
		}

		private void OnDisable()
		{
			_networkDataManager.PlayerInfosChanged -= UpdateUI;
			_networkDataManager.RolesSetupChanged -= UpdateUI;
			_networkDataManager.GameSetupReadyChanged -= UpdateUI;
			_networkDataManager.InvalidRolesSetupReceived -= OnInvalidRolesSetupReceived;
			_roomMenu.KickPlayerClicked -= OnKickPlayerClicked;
			_roomMenu.ChangeNicknameClicked -= OnChangeNicknameClicked;
			_settingsMenu.RolesSetupChanged -= OnRolesSetupChanged;
			_settingsMenu.GameSpeedChanged -= OnGameSpeedChanged;

			_roomMenu.Cleanup();
			_settingsMenu.Cleanup();
		}
	}
}
