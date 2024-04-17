using Fusion;
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
		private Button _leaveRoomBtn;

		private NetworkDataManager _networkDataManager;

		private PlayerRef _localPlayer;

		private int _minPlayer = -1;

		public void SetNetworkDataManager(NetworkDataManager networkDataManager, PlayerRef localPlayer)
		{
			_networkDataManager = networkDataManager;
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

			foreach (KeyValuePair<PlayerRef, Network.PlayerInfo> playerInfo in _networkDataManager.PlayerInfos)
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
			_leaveRoomBtn.interactable = !_networkDataManager.RolesSetupReady;
		}

		private void ShowInvalidRolesSetupWarning()
		{
			_warningText.text = "An invalid roles setup was sent to the server";
		}

		public void ClearWarning()
		{
			_warningText.text = "";
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