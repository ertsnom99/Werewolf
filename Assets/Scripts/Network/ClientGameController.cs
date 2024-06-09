using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using UnityEngine;
using Werewolf.Network.Configs;
using System.Collections;
using UnityEngine.SceneManagement;
using Werewolf.UI;

namespace Werewolf.Network
{
	[RequireComponent(typeof(NetworkRunner))]
	[SimulationBehaviour(Modes = SimulationModes.Client)]
	public class ClientGameController : MonoBehaviour, INetworkRunnerCallbacks
	{
		private NetworkRunner _networkRunner;
		private GameManager _gameManager;
		private UIManager _UIManager;

		private void Awake()
		{
			_networkRunner = GetComponent<NetworkRunner>();
		}

		public void OnSceneLoadDone(NetworkRunner runner)
		{
			switch (runner.SceneManager.MainRunnerScene.buildIndex)
			{
				case (int)SceneDefs.GAME:
					_UIManager = UIManager.Instance;
					_UIManager.SetFade(_UIManager.LoadingScreen, 1.0f);

					QuitScreen.QuitClicked += QuitGame;

					if (!GameManager.HasSpawned)
					{
						GameManager.ManagerSpawned += OnGameManagerSpawned;
					}
					else
					{
						OnGameManagerSpawned();
					}

					break;
			}
		}

		private void QuitGame()
		{
			_networkRunner.Shutdown();
		}

		private void OnGameManagerSpawned()
		{
			GameManager.ManagerSpawned -= OnGameManagerSpawned;

			_gameManager = GameManager.Instance;
			_UIManager.LoadingScreen.Initialize(_gameManager.Config.LoadingScreenText);

			_gameManager.RoleReceived += StartLoadingFade;
			StartCoroutine(ConfirmReadyToReceiveRole());
		}

		private IEnumerator ConfirmReadyToReceiveRole()
		{
			while (!_gameManager.Object.IsValid)
			{
				yield return 0;
			}

			_gameManager.RPC_ConfirmPlayerReadyToReceiveRole();
		}

		private void StartLoadingFade()
		{
			_gameManager.RoleReceived -= StartLoadingFade;

			_UIManager.LoadingScreen.FadeFinished += ConfirmReadyToPlay;
			_UIManager.LoadingScreen.Initialize("");
			_UIManager.FadeOut(_UIManager.LoadingScreen, _gameManager.Config.LoadingScreenTransitionDuration);
		}

		private void ConfirmReadyToPlay()
		{
			_UIManager.LoadingScreen.FadeFinished -= ConfirmReadyToPlay;

			_gameManager.RPC_ConfirmPlayerReadyToPlay();
		}

		public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
		{
			if (runner.SceneManager?.MainRunnerScene.buildIndex == (int)SceneDefs.GAME)
			{
				if (shutdownReason != ShutdownReason.Ok)
				{
					MainMenuManager.START_MESSAGE = $"Runner shutdown: {shutdownReason}";
				}

				SceneManager.LoadScene((int)SceneDefs.MENU);
			}
		}

		#region Unused Callbacks
		public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

		public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

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