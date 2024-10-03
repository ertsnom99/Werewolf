using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Threading.Tasks;
using Werewolf.Network;
using Werewolf.Data;
using Werewolf.UI;

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
		public static string START_MESSAGE = string.Empty;
		public static bool GAME_STARTED = false;
		public static string GAME_HISTORY;

		private void Start()
		{
			_joinMenu.JoinSessionClicked += JoinSession;
			_joinMenu.ReturnClicked += OpenMainMenu;
			_roomMenu.KickPlayerClicked += KickPlayer;
			_roomMenu.StartGameClicked += StartGame;
			_roomMenu.LeaveSessionClicked += LeaveSession;
			_rulesMenu.ReturnClicked += OpenMainMenu;
			_gameHistoryMenu.ReturnClicked += OpenMainMenu;

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
			START_MESSAGE = string.Empty;
			GAME_STARTED = false;
			GAME_HISTORY = default;
		}

		public void OpenJoinMenu(string message)
		{
			_joinMenu.Initialize(message);
			DisplayJoinMenu();
		}

		private void JoinSession()
		{
			_runner = GetRunner("Client");
			ConnectToServer(_runner, _joinMenu.GetSessionName());
		}

		private void OpenRoomMenu(bool setNickname = true)
		{
			if (!_networkDataManager)
			{
				NetworkDataManager.FinishedSpawning -= OnNetworkDataManagerFinishedSpawning;
				_networkDataManager = FindObjectOfType<NetworkDataManager>();
			}

			_networkDataManager.RolesSetupReadyChanged += _roomMenu.UpdatePlayerList;

			// TODO : Change min player everytime the leader select a new game setup
			_roomMenu.Initialize(_networkDataManager, _debugGameSetupData.MinPlayerCount, _runner.LocalPlayer, GAME_HISTORY);

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

		private void StartGame()
		{
			if (!_networkDataManager)
			{
				return;
			}

			// TODO : send the selected game setup
			_networkDataManager.RPC_SetRolesSetup(NetworkDataManager.ConvertToRolesSetup(_debugGameSetupData), _debugGameSetupData.MinPlayerCount);
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
		}

		private void DisplayJoinMenu()
		{
			_mainMenu.SetActive(false);
			_joinMenu.gameObject.SetActive(true);
			_roomMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(false);
		}

		private void DisplayRoomMenu()
		{
			_mainMenu.SetActive(false);
			_joinMenu.gameObject.SetActive(false);
			_roomMenu.gameObject.SetActive(true);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(false);
		}

		private void DisplayRulesMenu()
		{
			_mainMenu.SetActive(false);
			_joinMenu.gameObject.SetActive(false);
			_roomMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(true);
			_gameHistoryMenu.gameObject.SetActive(false);
		}

		private void DisplayGameHistoryMenu()
		{
			_mainMenu.SetActive(false);
			_joinMenu.gameObject.SetActive(false);
			_roomMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(true);
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
					OpenJoinMenu("");
					break;
				default:
					OpenJoinMenu($"Runner shutdown: {shutdownReason}");
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
					OpenJoinMenu($"Disconnected from server: You were kicked");
					break;
				default:
					OpenJoinMenu($"Disconnected from server: {reason}");
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

			OpenJoinMenu($"Connection failed: {reason}");
			CleanupNetwork();
		}

		private void CleanupNetwork()
		{
			if (_networkDataManager)
			{
				_networkDataManager.RolesSetupReadyChanged -= _roomMenu.UpdatePlayerList;
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

			_networkDataManager.RolesSetupReadyChanged -= _roomMenu.UpdatePlayerList;
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