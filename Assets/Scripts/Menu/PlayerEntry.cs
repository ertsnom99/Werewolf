using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Werewolf
{
    public class PlayerEntry : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField]
        private Image _leader;

        [SerializeField]
        private TMP_Text _nickname;

        public PlayerRef Player { get; private set; }

        public void SetPlayer(PlayerRef player)
        {
            Player = player;
            UpdateEntry();
        }

        public void UpdateEntry()
        {
            if (Player == null)
            {
                _leader.enabled = false;
                _nickname.text = "";
            }
            else
            {
                // TODO: check for first player
                _leader.enabled = Player.IsMasterClient;
                _nickname.text = Player.PlayerId.ToString();
            }
        }
    }
}