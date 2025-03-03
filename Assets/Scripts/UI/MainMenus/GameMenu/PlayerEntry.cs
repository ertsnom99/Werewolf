using Fusion;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Werewolf.UI
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
		private Button _kickButton;

		[SerializeField]
		private Color _currentPlayerColor = Color.yellow;

		[SerializeField]
		private Color _otherPlayerColor = Color.white;

		public event Action<PlayerRef> KickPlayerClicked;

		private PlayerRef _player;

		public void Initialize(Network.NetworkPlayerInfo playerInfo, PlayerRef localPlayer, bool isOdd, bool isLocalPlayerLeader, bool canBeKick)
		{
			_player = playerInfo.PlayerRef;

			_background.color = isOdd ? _oddBackgroundColor : _evenBackgroundColor;
			_leader.enabled = playerInfo.IsLeader;
			_nicknameText.text = playerInfo.Nickname;
			_nicknameText.color = playerInfo.PlayerRef == localPlayer ? _currentPlayerColor : _otherPlayerColor;
			_kickButton.gameObject.SetActive(isLocalPlayerLeader && playerInfo.PlayerRef != localPlayer);
			_kickButton.interactable = canBeKick;
		}

		public void SetCanBeKick(bool canBeKick)
		{
			_kickButton.interactable = canBeKick;
		}

		public void OnKickPLayer()
		{
			KickPlayerClicked?.Invoke(_player);
		}
	}
}