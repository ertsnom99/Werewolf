using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;
using Werewolf.Network;
using System;

namespace Werewolf.UI
{
	public class SettingsMenu : MonoBehaviour
	{
		[Header("Game Speed")]
		[SerializeField]
		private TMP_Dropdown _gameSpeedDropdown;

		[SerializeField]
		private LocalizeDropdown _gameSpeedLocalizeDropdown;

		[SerializeField]
		private LocalizeStringEvent _gameSpeedText;

		private PlayerRef _localPlayer;

		private NetworkDataManager _networkDataManager;

		public GameSpeed GameSpeed() => (GameSpeed)_gameSpeedDropdown.value;

		public event Action<GameSpeed> GameSpeedChanged;

		public void Initialize(NetworkDataManager networkDataManager, PlayerRef localPlayer)
		{
			_networkDataManager = networkDataManager;
			_localPlayer = localPlayer;

			OnPlayerInfosChanged();

			_networkDataManager.PlayerInfosChanged += OnPlayerInfosChanged;
			_networkDataManager.GameSpeedChanged += ChangeGameSpeed;
			_networkDataManager.GameSetupReadyChanged += OnGameSetupReadyChanged;
		}

		private void OnPlayerInfosChanged()
		{
			bool isLocalPlayerLeader = _networkDataManager.PlayerInfos.TryGet(_localPlayer, out PlayerNetworkInfo localPlayerInfo) && localPlayerInfo.IsLeader;

			_gameSpeedDropdown.gameObject.SetActive(isLocalPlayerLeader);
			_gameSpeedText.gameObject.SetActive(!isLocalPlayerLeader);
			ChangeGameSpeed(_networkDataManager.GameSpeed);
		}

		public void OnChangeGameSpeed(int gameSpeed)
		{
			GameSpeedChanged?.Invoke((GameSpeed)gameSpeed);
		}

		private void ChangeGameSpeed(GameSpeed gameSpeed)
		{
			_gameSpeedDropdown.value = (int)gameSpeed;
			_gameSpeedText.StringReference = _gameSpeedLocalizeDropdown.GetLocalizedValue();
		}

		private void OnGameSetupReadyChanged()
		{
			_gameSpeedDropdown.interactable = false;
		}

		public void UnregisterAll()
		{
			_networkDataManager.PlayerInfosChanged -= OnPlayerInfosChanged;
			_networkDataManager.GameSpeedChanged -= ChangeGameSpeed;
			_networkDataManager.GameSetupReadyChanged -= OnGameSetupReadyChanged;
		}
	}
}
