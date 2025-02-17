using System;
using Fusion;
using UnityEngine;
using UnityEngine.UI;
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
		private GameObject _warning;

		[SerializeField]
		private Button _startGameBtn;

		[SerializeField]
		private Button _leaveGameBtn;

		private PlayerRef _localPlayer;
		private int _minPlayer = -1;

		private NetworkDataManager _networkDataManager;

		public event Action<PlayerRef> KickPlayerClicked;
		public event Action<PlayerRef, string> ChangeNicknameClicked;
		public event Action<GameSpeed> GameSpeedChanged;
		public event Action<GameSpeed> StartGameClicked;
		public event Action LeaveGameClicked;

		public void Initialize(NetworkDataManager networkDataManager, PlayerRef localPlayer, int minPlayer, int minNicknameCharacterCount, string gameHistory)
		{
			_networkDataManager = networkDataManager;
			_localPlayer = localPlayer;
			_minPlayer = minPlayer;
			
			if (!_networkDataManager || _localPlayer == null)
			{
				return;
			}

			bool historyAvailable = !string.IsNullOrEmpty(gameHistory);
			_historyBtn.interactable = historyAvailable;

			_roomMenu.Initialize(networkDataManager, localPlayer, minNicknameCharacterCount);
			_settingsMenu.Initialize(networkDataManager, localPlayer);

			if (historyAvailable)
			{
				_historyMenu.Initialize(gameHistory);
			}

			UpdateGameButtons();

			_networkDataManager.PlayerInfosChanged += UpdateGameButtons;
			_networkDataManager.GameSetupReadyChanged += UpdateGameButtons;
			_networkDataManager.InvalidRolesSetupReceived += OnInvalidRolesSetupReceived;
			_roomMenu.KickPlayerClicked += OnKickPlayerClicked;
			_roomMenu.ChangeNicknameClicked += OnChangeNicknameClicked;
			_settingsMenu.GameSpeedChanged += OnGameSpeedChanged;
		}

		private void UpdateGameButtons()
		{
			bool localPlayerInfoExist = _networkDataManager.PlayerInfos.TryGet(_localPlayer, out PlayerNetworkInfo localPlayerInfo);
			bool isLocalPlayerLeader = localPlayerInfoExist && localPlayerInfo.IsLeader;

			_startGameBtn.interactable = isLocalPlayerLeader
										&& _minPlayer > -1
										&& _networkDataManager.PlayerInfos.Count >= _minPlayer
										&& !_networkDataManager.GameSetupReady;
			_leaveGameBtn.interactable = !_networkDataManager.GameSetupReady;
		}

		private void OnInvalidRolesSetupReceived()
		{
			DisplayInvalidRolesSetupWarning(true);
		}

		private void DisplayInvalidRolesSetupWarning(bool display)
		{
			_warning.SetActive(display);
		}

		private void OnKickPlayerClicked(PlayerRef player)
		{
			KickPlayerClicked?.Invoke(player);
		}

		private void OnChangeNicknameClicked(PlayerRef player, string nickname)
		{
			ChangeNicknameClicked?.Invoke(player, nickname);
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
			DisplayInvalidRolesSetupWarning(false);
			StartGameClicked?.Invoke(_settingsMenu.GameSpeed()); // TODO: Send all the settings together
		}

		public void OnLeaveGame()
		{
			LeaveGameClicked?.Invoke();
		}

		private void OnDisable()
		{
			_networkDataManager.PlayerInfosChanged -= UpdateGameButtons;
			_networkDataManager.GameSetupReadyChanged -= UpdateGameButtons;
			_networkDataManager.InvalidRolesSetupReceived -= OnInvalidRolesSetupReceived;
			_roomMenu.KickPlayerClicked -= OnKickPlayerClicked;
			_roomMenu.ChangeNicknameClicked -= OnChangeNicknameClicked;
			_settingsMenu.GameSpeedChanged -= OnGameSpeedChanged;

			_roomMenu.UnregisterAll();
			_settingsMenu.UnregisterAll();

			DisplayInvalidRolesSetupWarning(false);
		}
	}
}
