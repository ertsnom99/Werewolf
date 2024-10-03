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
		private TMP_Text _warningText;

		[SerializeField]
		private Button _startGameBtn;

		[SerializeField]
		private Button _leaveSessionBtn;

		private NetworkDataManager _networkDataManager;

		private PlayerRef _localPlayer;

		private int _minPlayer = -1;

		public event Action<PlayerRef> KickPlayerClicked;
		public event Action StartGameClicked;
		public event Action LeaveSessionClicked;

		public void Initialize(NetworkDataManager networkDataManager, int minPlayer, PlayerRef localPlayer, string gameHistory)
		{
			_networkDataManager = networkDataManager;
			_minPlayer = minPlayer;
			_localPlayer = localPlayer;

			if (!_networkDataManager || _localPlayer == null)
			{
				return;
			}

			_networkDataManager.PlayerInfosChanged += UpdatePlayerList;
			_networkDataManager.InvalidRolesSetupReceived += ShowInvalidRolesSetupWarning;
			UpdatePlayerList();

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

			_startGameBtn.interactable = isLocalPlayerLeader
										&& _minPlayer > -1
										&& _networkDataManager.PlayerInfos.Count >= _minPlayer
										&& !_networkDataManager.RolesSetupReady;
			_leaveSessionBtn.interactable = !_networkDataManager.RolesSetupReady;
		}

		private void OnKickPlayer(PlayerRef kickedPlayer)
		{
			KickPlayerClicked?.Invoke(kickedPlayer);
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
			StartGameClicked?.Invoke();
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
			_networkDataManager.InvalidRolesSetupReceived -= ShowInvalidRolesSetupWarning;
		}
	}
}