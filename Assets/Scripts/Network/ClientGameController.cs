using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using UnityEngine;
using Werewolf.Network.Configs;
using System.Collections;
using Werewolf.Data;

namespace Werewolf.Network
{
    [SimulationBehaviour(Modes = SimulationModes.Client)]
    public class ClientGameController : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Config")]
        [SerializeField]
        private GameConfig _gameConfig;

        private LoadingScreen _loadingScreen;

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            switch (runner.SceneManager.MainRunnerScene.buildIndex)
            {
                case (int)SceneDefs.GAME:
                    _loadingScreen = UIManager.Instance.LoadingScreen;

                    _loadingScreen.SetText(_gameConfig.LoadingScreenText);
                    _loadingScreen.gameObject.SetActive(true);

                    if (!GameManager.HasSpawned)
                    {
                        GameManager.OnSpawned += OnGameManagerSpawned;
                    }
                    else
                    {
                        OnGameManagerSpawned();
                    }

                    break;
            }
        }

        private void OnGameManagerSpawned()
        {
            StartCoroutine(ConfirmReadyToReceiveRole());
            GameManager.Instance.OnRoleReceived += StartLoadingFade;
        }

        private IEnumerator ConfirmReadyToReceiveRole()
        {
            while (!GameManager.Instance.Object.IsValid)
            {
                yield return 0;
            }

            GameManager.Instance.RPC_ConfirmPlayerReadyToReceiveRole();
        }

        private void StartLoadingFade()
        {
            _loadingScreen.OnFadeInOver += ConfirmReadyToPlay;
            _loadingScreen.FadeIn();
        }

        private void ConfirmReadyToPlay()
        {
            _loadingScreen.OnFadeInOver -= ConfirmReadyToPlay;
            GameManager.Instance.RPC_ConfirmPlayerReadyToPlay();
        }

        #region Unused Callbacks
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }

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