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

		private void Start()
		{
			_joinMenu.JoinSessionClicked += JoinSession;
			_joinMenu.ReturnClicked += OpenMainMenu;
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

		public void OpenRoomMenu(bool setNickname = true)
		{
			if (!_networkDataManager)
			{
				NetworkDataManager.FinishedSpawning -= OnNetworkDataManagerFinishedSpawning;
				_networkDataManager = FindObjectOfType<NetworkDataManager>();
			}

			_networkDataManager.RolesSetupReadyChanged += _roomMenu.UpdatePlayerList;

			// TODO : Change min player everytime the leader select a new game setup
			_roomMenu.Initialize(_networkDataManager, _debugGameSetupData.MinPlayerCount, _runner.LocalPlayer);

			if (setNickname)
			{
				_networkDataManager.RPC_SetPlayerNickname(_runner.LocalPlayer, _joinMenu.GetNickname());
			}

			DisplayRoomMenu();
		}

		public void StartGame()
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
			if (_networkDataManager)
			{
				_networkDataManager.RolesSetupReadyChanged -= _roomMenu.UpdatePlayerList;
			}

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
			_mainMenu.gameObject.SetActive(true);
			_joinMenu.gameObject.SetActive(false);
			_roomMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(false);
		}

		private void DisplayJoinMenu()
		{
			_mainMenu.gameObject.SetActive(false);
			_joinMenu.gameObject.SetActive(true);
			_roomMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(false);
		}

		private void DisplayRoomMenu()
		{
			_mainMenu.gameObject.SetActive(false);
			_joinMenu.gameObject.SetActive(false);
			_roomMenu.gameObject.SetActive(true);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(false);
		}

		private void DisplayRulesMenu()
		{
			_mainMenu.gameObject.SetActive(false);
			_joinMenu.gameObject.SetActive(false);
			_roomMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(true);
			_gameHistoryMenu.gameObject.SetActive(false);
		}

		private void DisplayGameHistoryMenu()
		{
			_mainMenu.gameObject.SetActive(false);
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

		public void OnConnectedToServer(NetworkRunner runner)
		{
			Log.Info($"{nameof(OnConnectedToServer)}: {nameof(runner.CurrentConnectionType)}: {runner.CurrentConnectionType}, {nameof(runner.LocalPlayer)}: {runner.LocalPlayer}");

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

		public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
		{
			Log.Info($"{nameof(OnShutdown)}: {nameof(shutdownReason)}: {shutdownReason}");

			switch (shutdownReason)
			{
				case ShutdownReason.Ok:
					OpenJoinMenu("");
					break;
				default:
					OpenJoinMenu($"Runner shutdown: {shutdownReason}");
					break;
			}
		}

		public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
		{
			Log.Info($"{nameof(OnDisconnectedFromServer)} - {reason}: {nameof(runner.LocalPlayer)}: {runner.LocalPlayer}");
			OpenJoinMenu($"Disconnected from server: {reason}");
		}

		public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
		{
			Log.Info($"{nameof(OnConnectFailed)}: {nameof(remoteAddress)}: {remoteAddress}, {nameof(reason)}: {reason}");
			OpenJoinMenu($"Connection failed: {reason}");
		}

		public void OnSceneLoadStart(NetworkRunner runner)
		{
			GAME_STARTED = true;
		}
		#endregion

		#region Unused Callbacks
		public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

		public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

		public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

		public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

		public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

		public void OnInput(NetworkRunner runner, NetworkInput input) { }

		public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

		public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

		public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

		public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

		public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

		public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

		public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

		public void OnSceneLoadDone(NetworkRunner runner) { }
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