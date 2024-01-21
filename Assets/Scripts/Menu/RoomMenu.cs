using Fusion;
using System.Collections.Generic;
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
        private Button _startGameBtn;

        [SerializeField]
        private Button _leaveRoomBtn;

        private PlayersData _playersData;

        private PlayerRef _localPlayer;

        private int _minPlayer = 2;

        public void SetPlayersData(PlayersData playersData, PlayerRef localPlayer)
        {
            _playersData = playersData;
            _localPlayer = localPlayer;

            if (!_playersData || _localPlayer == null)
            {
                return;
            }

            _playersData.OnPlayerNicknamesChanged += UpdatePlayerList;
            UpdatePlayerList();
        }

        public void SetMinPlayer(int minPlayer)
        {
            _minPlayer = minPlayer;
        }

        private void UpdatePlayerList()
        {
            if (_playersData == null || _localPlayer == null)
            {
                return;
            }

            // Clear list
            for (int i = _playerEntries.childCount - 1; i >= 0; i--)
            {
                Transform entry = _playerEntries.GetChild(i);

                entry.transform.parent = null;
                Destroy(entry.gameObject);
            }

            // Fill list
            bool localPlayerIsLeader = false;

            foreach (KeyValuePair<PlayerRef, PlayerData> playerData in _playersData.PlayerDatas)
            {
                PlayerEntry playerEntry = Instantiate(_playerEntryPrefab, _playerEntries);
                playerEntry.SetPlayerData(playerData.Value, _localPlayer);

                if (playerData.Value.PlayerRef == _localPlayer)
                {
                    localPlayerIsLeader = playerData.Value.IsLeader;
                }
            }

            // Update buttons
            _startGameBtn.interactable = localPlayerIsLeader && _minPlayer > -1 && _playerEntries.childCount >= _minPlayer;
            _leaveRoomBtn.interactable = true;
        }


    }
}