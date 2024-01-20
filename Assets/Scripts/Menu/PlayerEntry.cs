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
        private Image _leader;

        [SerializeField]
        private TMP_Text _nickname;

        [SerializeField]
        private Color _currentPlayerColor = Color.yellow;

        [SerializeField]
        private Color _otherPlayerColor = Color.white;

        public void SetPlayerData(PlayerData playerData, PlayerRef localPlayer)
        {
            _nickname.text = playerData.Nickname;
            _nickname.color = playerData.PlayerRef == localPlayer ? _currentPlayerColor : _otherPlayerColor;
            _leader.enabled = playerData.IsFirst;
        }
    }
}