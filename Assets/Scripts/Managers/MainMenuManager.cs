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

namespace Werewolf.Managers
{
	public class MainMenuManager : MonoBehaviour, INetworkRunnerCallbacks
	{
		[Header("Configs")]
		[SerializeField]
		private GameConfig _gameConfig;

		[Header("Menu")]
		[SerializeField]
		private GameObject _mainMenu;

		[SerializeField]
		private JoinMenu _joinMenu;

		[SerializeField]
		private GameMenu _gameMenu;

		[SerializeField]
		private RulesMenu _rulesMenu;

		[SerializeField]
		private GameHistoryMenu _gameHistoryMenu;

		[SerializeField]
		private OptionsMenu _optionsMenu;

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

		private static NetworkRunner _runner;

		private NetworkDataManager _networkDataManager;

		public static bool JUST_OPEN = true;
		public static LocalizedString START_MESSAGE = null;
		public static bool GAME_STARTED = false;
		public static string GAME_HISTORY;

		private void Start()
		{
			_joinMenu.JoinSessionClicked += JoinSession;
			_joinMenu.ReturnClicked += OpenMainMenu;
			_gameMenu.KickPlayerClicked += KickPlayer;
			_gameMenu.ChangeNicknameClicked += ChangeNickname;
			_gameMenu.RolesSetupChanged += ChangeRolesSetup;
			_gameMenu.GameSpeedChanged += ChangeGameSpeed;
			_gameMenu.StartGameClicked += StartGame;
			_gameMenu.LeaveGameClicked += LeaveGame;
			_rulesMenu.ReturnClicked += OpenMainMenu;
			_gameHistoryMenu.ReturnClicked += OpenMainMenu;
			_optionsMenu.ReturnClicked += OpenMainMenu;

			if (_runner)
			{
				_runner.AddCallbacks(this);
				OpenGameMenu(false);
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
			_joinMenu.Initialize(message, _gameConfig.MinNicknameCharacterCount);
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

			_joinMenu.Initialize(_joinFailedLocalizedString, _gameConfig.MinNicknameCharacterCount);

			Debug.Log($"Join failed: {connection.Result.ShutdownReason}");
		}

		private void OpenGameMenu(bool setNickname = true)
		{
			if (!_networkDataManager)
			{
				NetworkDataManager.FinishedSpawning -= OnNetworkDataManagerFinishedSpawning;
				_networkDataManager = NetworkDataManager.Instance;
			}

			_gameMenu.Initialize(_networkDataManager, _gameConfig, _runner.LocalPlayer, GAME_HISTORY);

			if (setNickname)
			{
				_networkDataManager.RPC_SetPlayerNickname(_runner.LocalPlayer, _joinMenu.GetNickname());
			}

			DisplayGameMenu();
		}

		private void KickPlayer(PlayerRef kickedPlayer)
		{
			if (!_networkDataManager.GameSetupReady)
			{
				_networkDataManager.RPC_KickPlayer(kickedPlayer);
			}
		}

		private void ChangeNickname(PlayerRef renamedPlayer, string nickname)
		{
			if (!_networkDataManager.GameSetupReady)
			{
				_networkDataManager.RPC_SetPlayerNickname(renamedPlayer, nickname);
			}
		}

		private void ChangeRolesSetup(int[] mandatoryRoleIDs, int[] optionalRoleIDs)
		{
			if (!_networkDataManager.GameSetupReady)
			{
				_networkDataManager.RPC_SetRolesSetup(mandatoryRoleIDs, optionalRoleIDs);
			}
		}

		private void ChangeGameSpeed(GameSpeed gameSpeed)
		{
			if (!_networkDataManager.GameSetupReady)
			{
				_networkDataManager.RPC_SetGameSpeed(gameSpeed);
			}
		}

		private void StartGame()
		{
			if (!_networkDataManager)
			{
				return;
			}

			_networkDataManager.RPC_SetGameSetupReady();
		}

		private void OnNetworkDataManagerFinishedSpawning()
		{
			OpenGameMenu();
		}

		private void LeaveGame()
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

		public void OpenOptionsMenu()
		{
			DisplayOptionsMenu();
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
			_gameMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(false);
			_optionsMenu.gameObject.SetActive(false);
		}

		private void DisplayJoinMenu()
		{
			_mainMenu.SetActive(false);
			_joinMenu.gameObject.SetActive(true);
			_gameMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(false);
			_optionsMenu.gameObject.SetActive(false);
		}

		private void DisplayGameMenu()
		{
			_mainMenu.SetActive(false);
			_joinMenu.gameObject.SetActive(false);
			_gameMenu.gameObject.SetActive(true);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(false);
			_optionsMenu.gameObject.SetActive(false);
		}

		private void DisplayRulesMenu()
		{
			_mainMenu.SetActive(false);
			_joinMenu.gameObject.SetActive(false);
			_gameMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(true);
			_gameHistoryMenu.gameObject.SetActive(false);
			_optionsMenu.gameObject.SetActive(false);
		}

		private void DisplayGameHistoryMenu()
		{
			_mainMenu.SetActive(false);
			_joinMenu.gameObject.SetActive(false);
			_gameMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(true);
			_optionsMenu.gameObject.SetActive(false);
		}

		private void DisplayOptionsMenu()
		{
			_mainMenu.SetActive(false);
			_joinMenu.gameObject.SetActive(false);
			_gameMenu.gameObject.SetActive(false);
			_rulesMenu.gameObject.SetActive(false);
			_gameHistoryMenu.gameObject.SetActive(false);
			_optionsMenu.gameObject.SetActive(true);
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

		private void OnDestroy()
		{
			if (_runner)
			{
				_runner.RemoveCallbacks(this);
			}
		}
	}
}