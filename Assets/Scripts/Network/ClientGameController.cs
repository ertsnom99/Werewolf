using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using UnityEngine;
using Werewolf.Network.Configs;
using System.Collections;
using UnityEngine.SceneManagement;
using Werewolf.UI;
using UnityEngine.Localization;
using Werewolf.Managers;

namespace Werewolf.Network
{
	[RequireComponent(typeof(NetworkRunner))]
	[SimulationBehaviour(Modes = SimulationModes.Client)]
	public class ClientGameController : MonoBehaviour, INetworkRunnerCallbacks
	{
		[Header("Localization")]
		[SerializeField]
		private LocalizedString _disconnectedLocalizedString;

		private NetworkRunner _networkRunner;
		private GameManager _gameManager;
		private UIManager _UIManager;

		private void Awake()
		{
			_networkRunner = GetComponent<NetworkRunner>();
		}

		void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
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
			_UIManager.LoadingScreen.Initialize(_gameManager.Config.WaitingForServerText);

			_gameManager.PlayerInitialized += StartLoadingFade;
			StartCoroutine(ConfirmReadyToBeInitialized());
		}

		private IEnumerator ConfirmReadyToBeInitialized()
		{
			while (!_gameManager.Object.IsValid)
			{
				yield return 0;
			}

			_gameManager.RPC_ConfirmPlayerReadyToBeInitialized();
		}

		private void StartLoadingFade()
		{
			_gameManager.PlayerInitialized -= StartLoadingFade;

			_UIManager.LoadingScreen.FadeFinished += ConfirmReadyToPlay;
			_UIManager.LoadingScreen.Initialize(null);
			_UIManager.FadeOut(_UIManager.LoadingScreen, _gameManager.Config.LoadingScreenTransitionDuration);
		}

		private void ConfirmReadyToPlay(FadingScreen fadingScreen, float opacity)
		{
			_UIManager.LoadingScreen.FadeFinished -= ConfirmReadyToPlay;

			_gameManager.RPC_ConfirmPlayerReadyToPlay();
		}

		void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
		{
			if (runner.SceneManager != null && runner.SceneManager.MainRunnerScene.buildIndex == (int)SceneDefs.GAME)
			{
				if (shutdownReason != ShutdownReason.Ok)
				{
					MainMenuManager.START_MESSAGE = _disconnectedLocalizedString;

					Debug.Log($"Runner shutdown: {shutdownReason}");
				}

				SceneManager.LoadScene((int)SceneDefs.MENU);
			}
		}

		#region Unused Callbacks
		void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

		void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

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