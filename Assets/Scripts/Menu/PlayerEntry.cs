using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Werewolf.Network;

namespace Werewolf
{
    public class PlayerEntry : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField]
        private Image _background;

        [SerializeField]
        private Color _oddBackgroundColor;

        [SerializeField]
        private Color _evenBackgroundColor;

        [SerializeField]
        private Image _leader;

        [SerializeField]
        private TMP_Text _nickname;

        [SerializeField]
        private Color _currentPlayerColor = Color.yellow;

        [SerializeField]
        private Color _otherPlayerColor = Color.white;

        public void SetPlayerData(PlayerInfo playerInfo, PlayerRef localPlayer)
        {
            _background.color = transform.GetSiblingIndex() % 2 > 0 ? _oddBackgroundColor : _evenBackgroundColor;
            _nickname.text = playerInfo.Nickname;
            _nickname.color = playerInfo.PlayerRef == localPlayer ? _currentPlayerColor : _otherPlayerColor;
            _leader.enabled = playerInfo.IsLeader;
        }
    }
}