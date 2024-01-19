using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

        private int _minPlayer = -1;

        public void SetMinPlayer(int minPlayer)
        {
            _minPlayer = minPlayer;
        }

        // TODO
        public void FillList()
        {
            ClearList();

            /*if (PhotonNetwork.CurrentRoom == null)
            {
                return;
            }

            foreach (KeyValuePair<int, Player> player in PhotonNetwork.CurrentRoom.Players)
            {
                PlayerEntry playerEntry = Instantiate(_playerEntryPrefab, _playerEntries);
                playerEntry.SetPlayer(player.Value);
            }*/

            UpdateButtons();
        }

        public void ClearList()
        {
            for (int i = _playerEntries.childCount - 1; i >= 0; i--)
            {
                Destroy(_playerEntries.GetChild(i).gameObject);
            }
        }

        // TODO
        public void AddPlayer(Player player)
        {
            List<PlayerEntry> existingPlayerEntries = new List<PlayerEntry>(_playerEntries.GetComponentsInChildren<PlayerEntry>());

            /*foreach (PlayerEntry existingPlayerEntry in existingPlayerEntries)
            {
                if (existingPlayerEntry.Player == player)
                {
                    return;
                }
            }

            PlayerEntry playerEntry = Instantiate(_playerEntryPrefab, _playerEntries);
            playerEntry.SetPlayer(player);*/

            UpdateButtons();
        }

        // TODO
        public void RemovePlayer(Player player)
        {
            List<PlayerEntry> existingPlayerEntries = new List<PlayerEntry>(_playerEntries.GetComponentsInChildren<PlayerEntry>());

            /*foreach (PlayerEntry existingPlayerEntry in existingPlayerEntries)
            {
                if (existingPlayerEntry.Player == player)
                {
                    // Must unparent, or UpdateButtons() will use the wrong _playerEntries.childCount
                    existingPlayerEntry.transform.parent = null;
                    Destroy(existingPlayerEntry.gameObject);
                }
                else
                {
                    existingPlayerEntry.UpdateEntry();
                }
            }*/

            UpdateButtons();
        }

        // TODO
        private void UpdateButtons()
        {
            //_startGameBtn.interactable = PhotonNetwork.IsMasterClient && _minPlayer > -1 && _playerEntries.childCount >= _minPlayer;
            _leaveRoomBtn.interactable = true;
        }
    }
}