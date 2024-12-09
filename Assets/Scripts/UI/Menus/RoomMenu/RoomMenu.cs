using Fusion;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Werewolf.Network;
using static Werewolf.GameHistoryManager;

namespace Werewolf.UI
{
	public class RoomMenu : MonoBehaviour
	{
		[Header("Players")]
		[SerializeField]
		private Transform _playerEntries;

		[SerializeField]
		private PlayerEntry _playerEntryPrefab;

		[Header("Game History")]
		[SerializeField]
		private GameObject _gameHistoryUI;

		[SerializeField]
		private GameHistory _gameHistory;

		[Header("UI")]
		[SerializeField]
		private TMP_InputField _nicknameInputField;

		[SerializeField]
		private Button _nicknameButton;

		[SerializeField]
		private TMP_Dropdown _gameSpeedDropdown;

		[SerializeField]
		private TMP_Text _gameSpeedText;

		[SerializeField]
		private TMP_Text _warningText;

		[SerializeField]
		private Button _startGameBtn;

		[SerializeField]
		private Button _leaveSessionBtn;

		private NetworkDataManager _networkDataManager;

		private PlayerRef _localPlayer;
		private int _minPlayer = -1;
		private int _minNicknameCharacterCount;
		private bool _initializedNicknameInputField;

		public event Action<PlayerRef> KickPlayerClicked;
		public event Action<PlayerRef, string> ChangeNicknameClicked;
		public event Action<GameSpeed> GameSpeedChanged;
		public event Action<GameSpeed> StartGameClicked;
		public event Action LeaveSessionClicked;

		public void Initialize(NetworkDataManager networkDataManager, PlayerRef localPlayer, int minPlayer, int minNicknameCharacterCount, string gameHistory)
		{
			_networkDataManager = networkDataManager;
			_localPlayer = localPlayer;
			_minPlayer = minPlayer;

			if (!_networkDataManager || _localPlayer == null)
			{
				return;
			}

			_minNicknameCharacterCount = minNicknameCharacterCount;
			_nicknameInputField.text = string.Empty;

			UpdatePlayerList();

			_networkDataManager.PlayerInfosChanged += UpdatePlayerList;
			_networkDataManager.GameSpeedChanged += ChangeGameSpeed;
			_networkDataManager.InvalidRolesSetupReceived += ShowInvalidRolesSetupWarning;

			if (!string.IsNullOrEmpty(gameHistory) && GameHistoryManager.Instance.LoadGameHistorySaveFromJson(gameHistory, out GameHistorySave gameHistorySave))
			{
				_gameHistory.DisplayGameHistory(gameHistorySave);
				_gameHistoryUI.SetActive(true);
			}
			else
			{
				_gameHistoryUI.SetActive(false);
			}
		}

		public void SetMinPlayer(int minPlayer)
		{
			_minPlayer = minPlayer;
		}

		public void UpdatePlayerList()
		{
			if (!_networkDataManager || _localPlayer == null)
			{
				return;
			}

			bool isLocalPlayerLeader = _networkDataManager.PlayerInfos.ContainsKey(_localPlayer) && _networkDataManager.PlayerInfos[_localPlayer].IsLeader;

			// Clear list
			for (int i = _playerEntries.childCount - 1; i >= 0; i--)
			{
				Destroy(_playerEntries.GetChild(i).gameObject);
			}

			// Fill list
			bool isOdd = true;

			foreach (KeyValuePair<PlayerRef, PlayerNetworkInfo> playerInfo in _networkDataManager.PlayerInfos)
			{
				PlayerEntry playerEntry = Instantiate(_playerEntryPrefab, _playerEntries);
				playerEntry.Initialize(playerInfo.Value, _localPlayer, isOdd, isLocalPlayerLeader);
				playerEntry.KickPlayerClicked += OnKickPlayer;

				isOdd = !isOdd;
			}

			if (!_initializedNicknameInputField && _networkDataManager.PlayerInfos.ContainsKey(_localPlayer))
			{
				_nicknameInputField.text = _networkDataManager.PlayerInfos[_localPlayer].Nickname;
				_initializedNicknameInputField = true;
			}

			UpdateNicknameButton();

			_gameSpeedDropdown.gameObject.SetActive(isLocalPlayerLeader);
			_gameSpeedText.gameObject.SetActive(!isLocalPlayerLeader);
			ChangeGameSpeed(_networkDataManager.GameSpeed);

			_startGameBtn.interactable = isLocalPlayerLeader
										&& _minPlayer > -1
										&& _networkDataManager.PlayerInfos.Count >= _minPlayer
										&& !_networkDataManager.GameSetupReady;
			_leaveSessionBtn.interactable = !_networkDataManager.GameSetupReady;
		}

		public void UpdateNicknameButton()
		{
			_nicknameButton.interactable = _nicknameInputField.text.Length >= _minNicknameCharacterCount && !_networkDataManager.GameSetupReady;
		}

		private void OnKickPlayer(PlayerRef kickedPlayer)
		{
			KickPlayerClicked?.Invoke(kickedPlayer);
		}

		public void OnChangeNickname()
		{
			ChangeNicknameClicked?.Invoke(_localPlayer, _nicknameInputField.text);
		}

		public void OnChangeGameSpeed(int gameSpeed)
		{
			GameSpeedChanged?.Invoke((GameSpeed)gameSpeed);
		}

		private void ChangeGameSpeed(GameSpeed gameSpeed)
		{
			_gameSpeedDropdown.value = (int)gameSpeed;
			_gameSpeedText.text = _gameSpeedDropdown.options[(int)gameSpeed].text;
		}

		private void ShowInvalidRolesSetupWarning()
		{
			_warningText.text = "An invalid roles setup was sent to the server";
		}

		private void ClearWarning()
		{
			_warningText.text = "";
		}

		public void OnStartGame()
		{
			ClearWarning();
			StartGameClicked?.Invoke((GameSpeed)_gameSpeedDropdown.value);
		}

		public void OnLeaveSession()
		{
			LeaveSessionClicked?.Invoke();
		}

		private void OnDisable()
		{
			ClearWarning();

			if (!_networkDataManager)
			{
				return;
			}

			_networkDataManager.PlayerInfosChanged -= UpdatePlayerList;
			_networkDataManager.GameSpeedChanged -= ChangeGameSpeed;
			_networkDataManager.InvalidRolesSetupReceived -= ShowInvalidRolesSetupWarning;
		}
	}
}