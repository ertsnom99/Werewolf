using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
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

		public event Action<PlayerRef> PromotePlayerClicked;
		public event Action<PlayerRef> KickPlayerClicked;
		public event Action<PlayerRef, string> ChangeNicknameClicked;

		private PlayerRef _localPlayer;
		private int _minNicknameCharacterCount;
		private bool _initializedNicknameInputField;
		private readonly List<PlayerEntry> _playerEntriesPool = new();

		private NetworkDataManager _networkDataManager;

		public void Initialize(NetworkDataManager networkDataManager, PlayerRef localPlayer, int minNicknameCharacterCount)
		{
			_networkDataManager = networkDataManager;
			_localPlayer = localPlayer;
			_minNicknameCharacterCount = minNicknameCharacterCount;

			_nicknameInputField.text = string.Empty;
			_nicknameInputField.characterLimit = GameConfig.MAX_NICKNAME_CHARACTER_COUNT;

			OnPlayerInfosChanged();

			_networkDataManager.PlayerInfosChanged += OnPlayerInfosChanged;
			_networkDataManager.GameSetupReadyChanged += OnGameSetupReadyChanged;
		}

		private void OnPlayerInfosChanged()
		{
			if (!_networkDataManager || _localPlayer == null)
			{
				return;
			}

			for (int i = _playerEntries.childCount - 1; i >= 0; i--)
			{
				ReturnPlayerEntryToPool(_playerEntries.GetChild(i).GetComponent<PlayerEntry>());
			}

			bool isOdd = true;
			bool localPlayerInfoExist = _networkDataManager.PlayerInfos.TryGet(_localPlayer, out NetworkPlayerInfo localPlayerInfo);

			foreach (KeyValuePair<PlayerRef, NetworkPlayerInfo> playerInfo in _networkDataManager.PlayerInfos)
			{
				PlayerEntry playerEntry = GetPlayerEntryFromPool();
				playerEntry.transform.SetParent(_playerEntries);
				playerEntry.Initialize(playerInfo.Value, _localPlayer, isOdd, localPlayerInfoExist && localPlayerInfo.IsLeader, !_networkDataManager.GameSetupReady, !_networkDataManager.GameSetupReady);
				playerEntry.PromotePlayerClicked += OnPromotePlayer;
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

		private void OnGameSetupReadyChanged()
		{
			if (!_networkDataManager || _localPlayer == null)
			{
				return;
			}

			for (int i = _playerEntries.childCount - 1; i >= 0; i--)
			{
				PlayerEntry playerEntry = _playerEntries.GetChild(i).GetComponent<PlayerEntry>();
				playerEntry.SetCanBePromoted(!_networkDataManager.GameSetupReady);
				playerEntry.SetCanBeKick(!_networkDataManager.GameSetupReady);
			}

			UpdateNicknameButton();
		}

		#region PlayerEntry Pool
		private PlayerEntry GetPlayerEntryFromPool()
		{
			if (_playerEntriesPool.Count > 0)
			{
				PlayerEntry playerEntry = _playerEntriesPool.Last();
				_playerEntriesPool.RemoveAt(_playerEntriesPool.Count - 1);
				playerEntry.gameObject.SetActive(true);
				return playerEntry;
			}
			else
			{
				PlayerEntry playerEntry = Instantiate(_playerEntryPrefab, transform);
				return playerEntry;
			}
		}

		private void ReturnPlayerEntryToPool(PlayerEntry playerEntry)
		{
			playerEntry.transform.SetParent(transform);
			playerEntry.gameObject.SetActive(false);
			_playerEntriesPool.Add(playerEntry);
		}
		#endregion

		public void UpdateNicknameButton()
		{
			_nicknameButton.interactable = _nicknameInputField.text.Length >= _minNicknameCharacterCount &&!_networkDataManager.GameSetupReady;
		}

		private void OnPromotePlayer(PlayerRef promotedPlayer)
		{
			PromotePlayerClicked?.Invoke(promotedPlayer);
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
			_networkDataManager.PlayerInfosChanged -= OnPlayerInfosChanged;
			_networkDataManager.GameSetupReadyChanged -= OnGameSetupReadyChanged;
		}
	}
}