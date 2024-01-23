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

        private GameDataManager _gameDataManager;

        private PlayerRef _localPlayer;

        private int _minPlayer = 2;

        public void SetGameDataManager(GameDataManager gameDataManager, PlayerRef localPlayer)
        {
            _gameDataManager = gameDataManager;
            _localPlayer = localPlayer;

            if (!_gameDataManager || _localPlayer == null)
            {
                return;
            }

            _gameDataManager.OnPlayerNicknamesChanged += UpdatePlayerList;
            UpdatePlayerList();
        }

        public void SetMinPlayer(int minPlayer)
        {
            _minPlayer = minPlayer;
        }

        private void UpdatePlayerList()
        {
            if (_gameDataManager == null || _localPlayer == null)
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

            foreach (KeyValuePair<PlayerRef, PlayerInfo> playerInfo in _gameDataManager.PlayerInfos)
            {
                PlayerEntry playerEntry = Instantiate(_playerEntryPrefab, _playerEntries);
                playerEntry.SetPlayerData(playerInfo.Value, _localPlayer);

                if (playerInfo.Value.PlayerRef == _localPlayer)
                {
                    localPlayerIsLeader = playerInfo.Value.IsLeader;
                }
            }

            // Update buttons
            _startGameBtn.interactable = localPlayerIsLeader && _minPlayer > -1 && _playerEntries.childCount >= _minPlayer;
            _leaveRoomBtn.interactable = true;
        }


    }
}