using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Werewolf.Network
{
    [SimulationBehaviour(Modes = SimulationModes.Server)]
    public class ServerGameController : SimulationBehaviour, INetworkRunnerCallbacks
    {
        private readonly List<PlayerRef> _players = new List<PlayerRef>();

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
            {
                return;
            }

            _players.Add(player);

            Log.Info($"Player joined: {player}");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
            {
                return;
            }

            _players.Remove(player);

            Log.Info($"Player left: {player}");

            if (_players.Count <= 0)
            {
                Log.Info("Last player left, shutdown...");

                runner.Shutdown();
            }
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            // Quit application after the Server Shutdown
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

        public void OnSceneLoadDone(NetworkRunner runner) { }

        public void OnSceneLoadStart(NetworkRunner runner) { }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        #endregion
    }
}