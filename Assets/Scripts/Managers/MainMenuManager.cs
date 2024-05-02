using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Threading.Tasks;
using Werewolf.Network;
using Werewolf.Data;

namespace Werewolf
{
	public class MainMenuManager : MonoBehaviour, INetworkRunnerCallbacks
	{
		[Header("Menu")]
		[SerializeField]
		private MainMenu _mainMenu;

		[SerializeField]
		private RoomMenu _roomMenu;

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

		private void Awake()
		{
			_mainMenu.ResetMenu("");
			DisplayMainMenu();
		}

		private void Start()
		{
			if (_runner)
			{
				_runner.AddCallbacks(this);
				PrepareRoomMenu(false);
				DisplayRoomMenu();
			}
			else
			{
				_mainMenu.ResetMenu(START_MESSAGE);

				if (JUST_OPEN)
				{
					if (CommandLineUtilities.TryGetArg(out string nickname, "-nickname"))
					{
						_mainMenu.SetNickname(nickname);
					}

					if (CommandLineUtilities.TryGetArg(out string sessionName, "-sessionName"))
					{
						_mainMenu.SetSessionName(sessionName);
					}

					if (CommandLineUtilities.TryGetArg(out string _, "_autoJoin"))
					{
						JoinGame();
					}
				}

				START_MESSAGE = string.Empty;
			}

			JUST_OPEN = false;
		}

		public void JoinGame()
		{
			if (!_mainMenu)
			{
				Debug.LogError("_mainMenu of the MainMenuManager is null");
				return;
			}

			if (!_roomMenu)
			{
				Debug.LogError("_roomMenu of the MainMenuManager is null");
				return;
			}

			_mainMenu.DisableMenu("Joining session...");

			_runner = GetRunner("Client");
			ConnectToServer(_runner, _mainMenu.GetSessionName());
		}

		public void LeaveGame()
		{
			if (_networkDataManager)
			{
				_networkDataManager.OnRolesSetupReadyChanged -= OnRolesSetupReadyChanged;
			}

			_runner.Shutdown();
		}

		public void StartGame()
		{
			if (!_networkDataManager)
			{
				return;
			}

			_roomMenu.ClearWarning();
			// TODO : send the selected game setup
			_networkDataManager.RPC_SetRolesSetup(NetworkDataManager.ConvertToRolesSetup(_debugGameSetupData), _debugGameSetupData.MinPlayerCount);
		}

		private void OnNetworkDataManagerSpawned()
		{
			PrepareRoomMenu();
			DisplayRoomMenu();
		}

		private void PrepareRoomMenu(bool setNickname = true)
		{
			if (!_networkDataManager)
			{
				NetworkDataManager.OnSpawned -= OnNetworkDataManagerSpawned;
				_networkDataManager = FindObjectOfType<NetworkDataManager>();
			}

			_networkDataManager.OnRolesSetupReadyChanged += OnRolesSetupReadyChanged;

			// TODO : Change min player everytime the leader select a new game setup
			_roomMenu.SetMinPlayer(_debugGameSetupData.MinPlayerCount);
			_roomMenu.SetNetworkDataManager(_networkDataManager, _runner.LocalPlayer);

			if (setNickname)
			{
				_networkDataManager.RPC_SetPlayerNickname(_runner.LocalPlayer, _mainMenu.GetNickname());
			}
		}

		private void OnRolesSetupReadyChanged()
		{
			_roomMenu.UpdatePlayerList();
		}

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
				OnNetworkDataManagerSpawned();
			}
			else
			{
				NetworkDataManager.OnSpawned += OnNetworkDataManagerSpawned;
			}
		}

		public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
		{
			Log.Info($"{nameof(OnShutdown)}: {nameof(shutdownReason)}: {shutdownReason}");

			switch (shutdownReason)
			{
				case ShutdownReason.Ok:
					_mainMenu.ResetMenu("");
					DisplayMainMenu();
					break;
				default:
					_mainMenu.ResetMenu($"Runner shutdown: {shutdownReason}");
					DisplayMainMenu();
					break;
			}
		}

		public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
		{
			Log.Info($"{nameof(OnDisconnectedFromServer)} - {reason}: {nameof(runner.LocalPlayer)}: {runner.LocalPlayer}");

			_mainMenu.ResetMenu($"Disconnected from server: {reason}");
			DisplayMainMenu();
		}

		public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
		{
			Log.Info($"{nameof(OnConnectFailed)}: {nameof(remoteAddress)}: {remoteAddress}, {nameof(reason)}: {reason}");

			_mainMenu.ResetMenu($"Connection failed: {reason}");
			DisplayMainMenu();
		}
		#endregion

		#region UI
		private void DisplayMainMenu()
		{
			_mainMenu.gameObject.SetActive(true);
			_roomMenu.gameObject.SetActive(false);
		}

		private void DisplayRoomMenu()
		{
			_mainMenu.gameObject.SetActive(false);
			_roomMenu.gameObject.SetActive(true);
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

		public void OnSceneLoadStart(NetworkRunner runner) { }
		#endregion

		private void OnDisable()
		{
			if (!_networkDataManager)
			{
				return;
			}

			_networkDataManager.OnRolesSetupReadyChanged -= OnRolesSetupReadyChanged;
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