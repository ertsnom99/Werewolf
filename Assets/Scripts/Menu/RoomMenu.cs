using Fusion;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Werewolf.Network;

namespace Werewolf
{
	public class RoomMenu : MonoBehaviour
	{
		[Header("UI")]
		[SerializeField]
		private Transform _playerEntries;

		[SerializeField]
		private PlayerEntry _playerEntryPrefab;

		[SerializeField]
		private TMP_Text _warningText;

		[SerializeField]
		private Button _startGameBtn;

		[SerializeField]
		private Button _leaveSessionBtn;

		private NetworkDataManager _networkDataManager;

		private PlayerRef _localPlayer;

		private int _minPlayer = -1;

		public event Action StartGame;
		public event Action LeaveSession;

		public void Initialize(NetworkDataManager networkDataManager, int minPlayer, PlayerRef localPlayer)
		{
			_networkDataManager = networkDataManager;
			_minPlayer = minPlayer;
			_localPlayer = localPlayer;

			if (!_networkDataManager || _localPlayer == null)
			{
				return;
			}

			_networkDataManager.OnPlayerInfosChanged += UpdatePlayerList;
			_networkDataManager.OnInvalidRolesSetupReceived += ShowInvalidRolesSetupWarning;
			UpdatePlayerList();
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

			// Clear list
			for (int i = _playerEntries.childCount - 1; i >= 0; i--)
			{
				Destroy(_playerEntries.GetChild(i).gameObject);
			}

			// Fill list
			bool isOdd = true;
			bool localPlayerIsLeader = false;

			foreach (KeyValuePair<PlayerRef, Network.PlayerNetworkInfo> playerInfo in _networkDataManager.PlayerInfos)
			{
				PlayerEntry playerEntry = Instantiate(_playerEntryPrefab, _playerEntries);
				playerEntry.SetPlayerData(playerInfo.Value, _localPlayer, isOdd);

				if (playerInfo.Value.PlayerRef == _localPlayer)
				{
					localPlayerIsLeader = playerInfo.Value.IsLeader;
				}

				isOdd = !isOdd;
			}

			// Update buttons
			_startGameBtn.interactable = localPlayerIsLeader
										&& _minPlayer > -1
										&& _networkDataManager.PlayerInfos.Count >= _minPlayer
										&& !_networkDataManager.RolesSetupReady;
			_leaveSessionBtn.interactable = !_networkDataManager.RolesSetupReady;
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
			StartGame?.Invoke();
		}

		public void OnLeaveSession()
		{
			LeaveSession?.Invoke();
		}

		private void OnDisable()
		{
			ClearWarning();

			if (!_networkDataManager)
			{
				return;
			}

			_networkDataManager.OnPlayerInfosChanged -= UpdatePlayerList;
			_networkDataManager.OnInvalidRolesSetupReceived -= ShowInvalidRolesSetupWarning;
		}
	}
}