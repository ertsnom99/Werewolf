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
		private TMP_Text _nicknameText;

		[SerializeField]
		private Color _currentPlayerColor = Color.yellow;

		[SerializeField]
		private Color _otherPlayerColor = Color.white;

		public void SetPlayerData(Network.PlayerInfo playerInfo, PlayerRef localPlayer, bool isOdd)
		{
			_background.color = isOdd ? _oddBackgroundColor : _evenBackgroundColor;
			_nicknameText.text = playerInfo.Nickname;
			_nicknameText.color = playerInfo.PlayerRef == localPlayer ? _currentPlayerColor : _otherPlayerColor;
			_leader.enabled = playerInfo.IsLeader;
		}
	}
}