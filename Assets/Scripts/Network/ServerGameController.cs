using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Werewolf.Network.Configs;

namespace Werewolf.Network
{
	[SimulationBehaviour(Modes = SimulationModes.Server)]
	public class ServerGameController : SimulationBehaviour, INetworkRunnerCallbacks
	{
		[SerializeField]
		private NetworkDataManager _networkDataManagerPrefab;

		private static NetworkDataManager _networkDataManager;

		void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
		{
			switch (runner.SceneManager.MainRunnerScene.buildIndex)
			{
				case (int)SceneDefs.MENU:
					if (!_networkDataManager)
					{
						_networkDataManager = runner.Spawn(_networkDataManagerPrefab, Vector3.zero, Quaternion.identity);
						runner.AddCallbacks(_networkDataManager);
					}

					_networkDataManager.GameSetupReadyChanged += OnGameSetupReadyChanged;
					_networkDataManager.ClearRolesSetup();
					Runner.SessionInfo.IsOpen = true;

					break;
				case (int)SceneDefs.GAME:
					GameManager.Instance.PrepareGame(_networkDataManager.RolesSetup, _networkDataManager.GameSpeed);
					break;
			}
		}

		private void OnGameSetupReadyChanged()
		{
			if (!_networkDataManager.GameSetupReady)
			{
				return;
			}

			_networkDataManager.GameSetupReadyChanged -= OnGameSetupReadyChanged;

			Runner.SessionInfo.IsOpen = false;
			Runner.LoadScene(SceneRef.FromIndex((int)SceneDefs.GAME), LoadSceneMode.Single);
		}

		void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
		{
			Log.Info($"Player joined: {player}");
		}

		void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
		{
			Log.Info($"Player left: {player}");

			if (runner.ActivePlayers.Count() > 0 || runner.SceneManager.MainRunnerScene.buildIndex != (int)SceneDefs.GAME)
			{
				return;
			}

			Log.Info("Last player left, shutdown...");
			Runner.LoadScene(SceneRef.FromIndex((int)SceneDefs.MENU), LoadSceneMode.Single);
		}

		void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
		{
			Application.Quit(0);
		}

		#region Unused Callbacks
		void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }

		void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

		void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

		void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

		void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

		void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

		void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }

		void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

		void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

		void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

		void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

		void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

		void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }

		void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

		void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
		#endregion
	}
}