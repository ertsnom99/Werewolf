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
		private GameDataManager _gameDataManagerPrefab;

		private GameDataManager _gameDataManager;

		public void OnSceneLoadDone(NetworkRunner runner)
		{
			switch (runner.SceneManager.MainRunnerScene.buildIndex)
			{
				case (int)SceneDefs.MENU:
					_gameDataManager = runner.Spawn(_gameDataManagerPrefab, Vector3.zero, Quaternion.identity);
					_gameDataManager.OnGameDataReadyChanged += OnGameDataReadyChanged;
					runner.AddCallbacks(_gameDataManager);
					break;
				case (int)SceneDefs.GAME:
					GameManager.Instance.PrepareGame(_gameDataManager.RolesSetup);
					break;
			}
		}

		private void OnGameDataReadyChanged()
		{
			if (!_gameDataManager.GameDataReady)
			{
				return;
			}

			_gameDataManager.OnGameDataReadyChanged -= OnGameDataReadyChanged;

			Runner.SessionInfo.IsOpen = false;
			Runner.LoadScene(SceneRef.FromIndex((int)SceneDefs.GAME), LoadSceneMode.Single);
		}

		public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
		{
			if (!runner.IsServer)
			{
				return;
			}

			Log.Info($"Player joined: {player}");
		}

		public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
		{
			if (!runner.IsServer)
			{
				return;
			}

			// TODO: If a player leave after the game started, give him a mark for death
			Log.Info($"Player left: {player}");

			if (runner.ActivePlayers.Count() > 0)
			{
				return;
			}

			if (runner.SceneManager.MainRunnerScene.buildIndex == (int)SceneDefs.GAME)
			{
				Log.Info("Last player left, shutdown...");

				runner.Shutdown();
			}
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

		public void OnSceneLoadStart(NetworkRunner runner) { }

		public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

		public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
		#endregion
	}
}