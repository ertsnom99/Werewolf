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

		public void OnSceneLoadDone(NetworkRunner runner)
		{
			switch (runner.SceneManager.MainRunnerScene.buildIndex)
			{
				case (int)SceneDefs.MENU:
					if (!_networkDataManager)
					{
						_networkDataManager = runner.Spawn(_networkDataManagerPrefab, Vector3.zero, Quaternion.identity);
						runner.AddCallbacks(_networkDataManager);
					}

					_networkDataManager.OnRolesSetupReadyChanged += OnRolesSetupReadyChanged;
					_networkDataManager.ClearRolesSetup();
					Runner.SessionInfo.IsOpen = true;

					break;
				case (int)SceneDefs.GAME:
					runner.AddCallbacks(GameManager.Instance);
					GameManager.Instance.PrepareGame(_networkDataManager.RolesSetup);
					break;
			}
		}

		public void OnSceneLoadStart(NetworkRunner runner)
		{
			switch (runner.SceneManager.MainRunnerScene.buildIndex)
			{
				case (int)SceneDefs.GAME:
					runner.RemoveCallbacks(GameManager.Instance);
					break;
			}
		}

		private void OnRolesSetupReadyChanged()
		{
			if (!_networkDataManager.RolesSetupReady)
			{
				return;
			}

			_networkDataManager.OnRolesSetupReadyChanged -= OnRolesSetupReadyChanged;

			Runner.SessionInfo.IsOpen = false;
			Runner.LoadScene(SceneRef.FromIndex((int)SceneDefs.GAME), LoadSceneMode.Single);
		}

		public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
		{
			Log.Info($"Player joined: {player}");
		}

		public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
		{
			Log.Info($"Player left: {player}");

			if (runner.ActivePlayers.Count() > 0 || runner.SceneManager.MainRunnerScene.buildIndex != (int)SceneDefs.GAME)
			{
				return;
			}

			Log.Info("Last player left, shutdown...");
			Runner.LoadScene(SceneRef.FromIndex((int)SceneDefs.MENU), LoadSceneMode.Single);
		}

		public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
		{
			Application.Quit(0);
		}

		#region Unused Callbacks
		public void OnConnectedToServer(NetworkRunner runner) { }

		public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

		public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

		public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

		public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

		public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

		public void OnInput(NetworkRunner runner, NetworkInput input) { }

		public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

		public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

		public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

		public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

		public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

		public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

		public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
		#endregion
	}
}