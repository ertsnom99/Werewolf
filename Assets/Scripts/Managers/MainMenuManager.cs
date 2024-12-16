using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Threading.Tasks;
using Werewolf.Network;
using Werewolf.Data;
using Werewolf.UI;
using UnityEngine.Localization;

namespace Werewolf
{
	public class MainMenuManager : MonoBehaviour, INetworkRunnerCallbacks
	{
		[Header("Menu")]
		[SerializeField]
		private GameObject _mainMenu;

		[SerializeField]
		private JoinMenu _joinMenu;

		[SerializeField]
		private RoomMenu _roomMenu;

		[SerializeField]
		private RulesMenu _rulesMenu;

		[SerializeField]
		private GameHistoryMenu _gameHistoryMenu;

		[SerializeField]
		private SettingsMenu _settingsMenu;

		[Header("Localization")]
		[SerializeField]
		private LocalizedString _joinFailedLocalizedString;

		[SerializeField]
		private LocalizedString _disconnectedLocalizedString;

		[SerializeField]
		private LocalizedString _kickedLocalizedString;

		[SerializeField]
		private LocalizedString _connectionFailedLocalizedString;

		[Header("Network")]
		[SerializeField]
		private NetworkRunner _runnerPrefab;

		// TODO: when complete workflow is integrated, add a UNITY_EDITOR region
		[Header("Debug")]
		[SerializeField]
		private GameSetupData _debugGameSetupData;

		private static NetworkRunner _runner;

		private NetworkDataManager _networkDataManager;

		public static bool JUST_OPEN = true;
		public static LocalizedString START_MESSAGE = null;
		public static bool GAME_STARTED = false;
		public static string GAME_HISTORY;

		private readonly int MIN_NICKNAME_CHARACTER_COUNT = 3;

		private void Start()
		{
			_joinMenu.JoinSessionClicked += JoinSession;
			_joinMenu.ReturnClicked += OpenMainMenu;
			_roomMenu.KickPlayerClicked += KickPlayer;
			_roomMenu.ChangeNicknameClicked += ChangeNickname;
			_roomMenu.GameSpeedChanged += ChangeGameSpeed;
			_roomMenu.StartGameClicked += StartGame;
			_roomMenu.LeaveSessionClicked += LeaveSession;
			_rulesMenu.ReturnClicked += OpenMainMenu;
			_gameHistoryMenu.ReturnClicked += OpenMainMenu;
			_settingsMenu.ReturnClicked += OpenMainMenu;

			if (_runner)
			{
				_runner.AddCallbacks(this);
				OpenRoomMenu(false);
			}
			else
			{
				if (GAME_STARTED)
				{
					OpenJoinMenu(START_MESSAGE);
				}

				if (JUST_OPEN)
				{
					if (CommandLineUtilities.TryGetArg(out string nickname, "-nickname"))
					{
						_joinMenu.SetNickname(nickname);
					}

					if (CommandLineUtilities.TryGetArg(out string sessionName, "-sessionName"))
					{
						_joinMenu.SetSessionName(sessionName);
					}

					if (CommandLineUtilities.TryGetArg(out string _, "_autoJoin"))
					{
						_joinMenu.OnJoinSession();
					}
				}
			}

			JUST_OPEN = false;
			START_MESSAGE = null;
			GAME_STARTED = false;
			GAME_HISTORY = default;
		}

		public void OpenJoinMenu()
		{
			OpenJoinMenu(null);
		}

		private void OpenJoinMenu(LocalizedString message)
		{
			_joinMenu.Initialize(message, MIN_NICKNAME_CHARACTER_COUNT);
			DisplayJoinMenu();
		}

		private async void JoinSession()
		{
			if (!_runner)
			{
				_runner = GetRunner("Client");
			}

			Task<StartGameResult> connection = ConnectToServer(_runner, _joinMenu.GetSessionName());
			await connection;
			
			if (connection.Result.Ok)
			{
				return;
			}

			_joinMenu.Initialize(_joinFailedLocalizedString, MIN_NICKNAME_CHARACTER_COUNT);

			Debug.Log($"Join failed: {connection.Result.ShutdownReason}");
		}

		private void OpenRoomMenu(bool setNickname = true)
		{
			if (!_networkDataManager)
			{
				NetworkDataManager.FinishedSpawning -= OnNetworkDataManagerFinishedSpawning;
				_networkDataManager = FindObjectOfType<NetworkDataManager>();
			}

			_networkDataManager.GameSetupReadyChanged += _roomMenu.UpdatePlayerList;

			// TODO : Change min player everytime the leader select a new game setup
			_roomMenu.Initialize(_networkDataManager, _runner.LocalPlayer, _debugGameSetupData.MinPlayerCount, MIN_NICKNAME_CHARACTER_COUNT, GAME_HISTORY);

			if (setNickname)
			{
				_networkDataManager.RPC_SetPlayerNickname(_runner.LocalPlayer, _joinMenu.GetNickname());
			}

			DisplayRoomMenu();
		}

		private void KickPlayer(PlayerRef kickedPlayer)
		{
			_networkDataManager.RPC_KickPlayer(kickedPlayer);
		}

		private void ChangeNickname(PlayerRef renamedPlayer, string nickname)
		{
			_networkDataManager.RPC_SetPlayerNickname(renamedPlayer, nickname);
		}

		private void ChangeGameSpeed(GameSpeed gameSpeed)
		{
			_networkDataManager.RPC_SetGameSpeed(gameSpeed);
		}

		private void StartGame(GameSpeed gameSpeed)
		{
			if (!_networkDataManager)
			{
				return;
			}

			// TODO : send the selected game setup
			_networkDataManager.RPC_SetGameSetup(NetworkDataManager.ConvertToRolesSetup(_debugGameSetupData), gameSpeed, _debugGameSetupData.MinPlayerCount);
		}

		private void OnNetworkDataManagerFinishedSpawning()
		{
			OpenRoomMenu();
		}

		private void LeaveSession()
		{
			_runner.Shutdown();
		}

		public void OpenRulesMenu()
		{
			DisplayRulesMenu();
		}

		public void OpenGameHistoryMenu()
		{
			DisplayGameHistoryMenu();
		}

		public void OpenSettingsMenu()
		{
			DisplaySettingsMenu();
		}

		private void OpenMainMenu()
		{
			DisplayMainMenu();
		}

		public void Quit()
		{
			Application.Quit();
		}

		#region UI
		private void DisplayMainMenu()
		{
			_mainMenu.SetActive(true);
			_joinMenu.gameObject.SetActive(false);
			_roomMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(false);
			_settingsMenu.gameObject.SetActive(false);
		}

		private void DisplayJoinMenu()
		{
			_mainMenu.SetActive(false);
			_joinMenu.gameObject.SetActive(true);
			_roomMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(false);
			_settingsMenu.gameObject.SetActive(false);
		}

		private void DisplayRoomMenu()
		{
			_mainMenu.SetActive(false);
			_joinMenu.gameObject.SetActive(false);
			_roomMenu.gameObject.SetActive(true);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(false);
			_settingsMenu.gameObject.SetActive(false);
		}

		private void DisplayRulesMenu()
		{
			_mainMenu.SetActive(false);
			_joinMenu.gameObject.SetActive(false);
			_roomMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(true);
			_gameHistoryMenu.gameObject.SetActive(false);
			_settingsMenu.gameObject.SetActive(false);
		}

		private void DisplayGameHistoryMenu()
		{
			_mainMenu.SetActive(false);
			_joinMenu.gameObject.SetActive(false);
			_roomMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(true);
			_settingsMenu.gameObject.SetActive(false);
		}

		private void DisplaySettingsMenu()
		{
			_mainMenu.SetActive(false);
			_joinMenu.gameObject.SetActive(false);
			_roomMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(false);
			_settingsMenu.gameObject.SetActive(true);
		}
		#endregion

		#region Connection
		private NetworkRunner GetRunner(string name)
		{
			var runner = Instantiate(_runnerPrefab);
			runner.name = name;
			runner.AddCallbacks(this);

			return runner;
		}

		private Task<StartGameResult> ConnectToServer(NetworkRunner runner, string sessionName)
		{
			return runner.StartGame(new()
			{
				SessionName = sessionName,
				GameMode = GameMode.Client,
				SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
			});
		}

		void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnConnectedToServer)}: {nameof(runner.CurrentConnectionType)}: {runner.CurrentConnectionType}, {nameof(runner.LocalPlayer)}: {runner.LocalPlayer}");

			_networkDataManager = FindObjectOfType<NetworkDataManager>();

			if (_networkDataManager)
			{
				OnNetworkDataManagerFinishedSpawning();
			}
			else
			{
				NetworkDataManager.FinishedSpawning += OnNetworkDataManagerFinishedSpawning;
			}
		}

		void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnShutdown)}: {nameof(shutdownReason)}: {shutdownReason}");

			if (_joinMenu.gameObject.activeSelf)
			{
				return;
			}

			switch (shutdownReason)
			{
				case ShutdownReason.Ok:
					OpenJoinMenu(null);
					break;
				default:
					OpenJoinMenu(_disconnectedLocalizedString);
					Debug.Log($"Runner shutdown: {shutdownReason}");
					break;
			}

			CleanupNetwork();
		}

		void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnDisconnectedFromServer)} - {reason}: {nameof(runner.LocalPlayer)}: {runner.LocalPlayer}");

			if (_joinMenu.gameObject.activeSelf)
			{
				return;
			}

			switch (reason)
			{
				case NetDisconnectReason.Requested:
					OpenJoinMenu(_kickedLocalizedString);
					break;
				default:
					OpenJoinMenu(_disconnectedLocalizedString);
					Debug.Log($"Disconnected from server: {reason}");
					break;
			}

			CleanupNetwork();
		}

		void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnConnectFailed)}: {nameof(remoteAddress)}: {remoteAddress}, {nameof(reason)}: {reason}");

			if (_joinMenu.gameObject.activeSelf)
			{
				return;
			}

			OpenJoinMenu(_connectionFailedLocalizedString);
			Debug.Log($"Connection failed: {reason}");

			CleanupNetwork();
		}

		private void CleanupNetwork()
		{
			if (_networkDataManager)
			{
				_networkDataManager.GameSetupReadyChanged -= _roomMenu.UpdatePlayerList;
			}

			if (_runner)
			{
				_runner.Shutdown();
			}
		}

		void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner)
		{
			GAME_STARTED = true;
		}
		#endregion

		#region Unused Callbacks
		void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

		void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

		void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

		void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

		void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

		void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }

		void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

		void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

		void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

		void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

		void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

		void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

		void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

		void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }
		#endregion

		private void OnDisable()
		{
			if (!_networkDataManager)
			{
				return;
			}

			_networkDataManager.GameSetupReadyChanged -= _roomMenu.UpdatePlayerList;
		}

		private void OnDestroy()
		{
			if (!_runner)
			{
				return;
			}

			_runner.RemoveCallbacks(this);
		}
	}
}