using Fusion;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Werewolf.Data;
using Werewolf.Network;

namespace Werewolf.UI
{
	public class RoomMenu : MonoBehaviour
	{
		[Header("Players")]
		[SerializeField]
		private Transform _playerEntries;

		[SerializeField]
		private PlayerEntry _playerEntryPrefab;

		[Header("Nickname")]
		[SerializeField]
		private TMP_InputField _nicknameInputField;

		[SerializeField]
		private Button _nicknameButton;

		public event Action<PlayerRef> KickPlayerClicked;
		public event Action<PlayerRef, string> ChangeNicknameClicked;

		private PlayerRef _localPlayer;
		private int _minNicknameCharacterCount;
		private bool _initializedNicknameInputField;

		private NetworkDataManager _networkDataManager;

		public void Initialize(NetworkDataManager networkDataManager, PlayerRef localPlayer, int minNicknameCharacterCount)
		{
			_networkDataManager = networkDataManager;
			_localPlayer = localPlayer;
			_minNicknameCharacterCount = minNicknameCharacterCount;

			_nicknameInputField.text = string.Empty;
			_nicknameInputField.characterLimit = GameConfig.MAX_NICKNAME_CHARACTER_COUNT;

			UpdatePlayerList();

			_networkDataManager.PlayerInfosChanged += UpdatePlayerList;
			_networkDataManager.GameSetupReadyChanged += UpdatePlayerList;
		}

		private void UpdatePlayerList()
		{
			if (!_networkDataManager || _localPlayer == null)
			{
				return;
			}

			for (int i = _playerEntries.childCount - 1; i >= 0; i--)
			{
				Destroy(_playerEntries.GetChild(i).gameObject);
			}

			bool isOdd = true;
			bool localPlayerInfoExist = _networkDataManager.PlayerInfos.TryGet(_localPlayer, out NetworkPlayerInfo localPlayerInfo);

			foreach (KeyValuePair<PlayerRef, NetworkPlayerInfo> playerInfo in _networkDataManager.PlayerInfos)
			{
				PlayerEntry playerEntry = Instantiate(_playerEntryPrefab, _playerEntries);
				playerEntry.Initialize(playerInfo.Value, _localPlayer, isOdd, localPlayerInfoExist && localPlayerInfo.IsLeader, !_networkDataManager.GameSetupReady);
				playerEntry.KickPlayerClicked += OnKickPlayer;

				isOdd = !isOdd;
			}

			if (!_initializedNicknameInputField && localPlayerInfoExist)
			{
				_nicknameInputField.text = localPlayerInfo.Nickname;
				_initializedNicknameInputField = true;
			}

			UpdateNicknameButton();
		}

		public void UpdateNicknameButton()
		{
			_nicknameButton.interactable = _nicknameInputField.text.Length >= _minNicknameCharacterCount &&!_networkDataManager.GameSetupReady;
		}

		private void OnKickPlayer(PlayerRef kickedPlayer)
		{
			KickPlayerClicked?.Invoke(kickedPlayer);
		}

		public void OnChangeNickname()
		{
			ChangeNicknameClicked?.Invoke(_localPlayer, _nicknameInputField.text);
		}

		public void Cleanup()
		{
			_networkDataManager.PlayerInfosChanged -= UpdatePlayerList;
			_networkDataManager.GameSetupReadyChanged -= UpdatePlayerList;
		}
	}
}