using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Network.Configs;

namespace Werewolf.Network
{
    [SimulationBehaviour(Modes = SimulationModes.Server)]
    public class ServerGameController : SimulationBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField]
        private GameDataManager _gameDataManagerPrefab;

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (runner.SceneManager.MainRunnerScene.buildIndex != (int)SceneDefs.MENU)
            {
                return;
            }

            GameDataManager gameDataManager = runner.Spawn(_gameDataManagerPrefab, Vector3.zero, Quaternion.identity);
            runner.AddCallbacks(gameDataManager);
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