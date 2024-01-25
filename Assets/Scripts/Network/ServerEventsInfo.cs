using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Werewolf.Network
{
    public class ServerEventsInfo : SimulationBehaviour, INetworkRunnerCallbacks
    {
        private const int TIMEOUT = 5;
        private float TIME_COUNTER = TIMEOUT;

        private void Update()
        {
            TIME_COUNTER -= Time.deltaTime;

            if (TIME_COUNTER < 0)
            {
                TIME_COUNTER = TIMEOUT;

                if (Runner && Runner.IsServer)
                {
                    string msg = $"Total Players: {Runner.ActivePlayers.Count()}";

                    foreach (PlayerRef player in Runner.ActivePlayers)
                    {
                        msg += $"\n{player}: {Runner.GetPlayerConnectionType(player)}";
                    }

                   Log.Info(msg);
                }
            }
        }

        private void OnDestroy()
        {
            Log.Info($"{nameof(OnDestroy)}: {gameObject.name}");
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Log.Info($"{nameof(OnConnectedToServer)}: {nameof(runner.CurrentConnectionType)}: {runner.CurrentConnectionType}, {nameof(runner.LocalPlayer)}: {runner.LocalPlayer}");
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Log.Info($"{nameof(OnConnectFailed)}: {nameof(remoteAddress)}: {remoteAddress}, {nameof(reason)}: {reason}");
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            Log.Info($"{nameof(OnConnectRequest)}: {nameof(request.RemoteAddress)}: {request.RemoteAddress}");
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
            Log.Info($"{nameof(OnCustomAuthenticationResponse)}");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Log.Info($"{nameof(OnDisconnectedFromServer)} - {reason}: {nameof(runner.LocalPlayer)}: {runner.LocalPlayer}");
        }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
            Log.Info($"{nameof(OnHostMigration)}: {nameof(HostMigrationToken)}: {hostMigrationToken}");
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) { }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Log.Info($"{nameof(OnPlayerJoined)}: {nameof(player)}: {player}");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Log.Info($"{nameof(OnPlayerLeft)}: {nameof(player)}: {player}");
        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
            Log.Info($"{nameof(OnReliableDataReceived)}: {nameof(PlayerRef)}:{player}, {nameof(key)}:{key}, {nameof(data)}:{data.Count}");
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            Log.Info($"{nameof(OnSceneLoadDone)}: {nameof(runner.SceneManager.MainRunnerScene)}: {runner.SceneManager.MainRunnerScene.name}");
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
            Log.Info($"{nameof(OnSceneLoadStart)}: {nameof(runner.SceneManager.MainRunnerScene)}: {runner.SceneManager.MainRunnerScene.name}");
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            Log.Info($"{nameof(OnSessionListUpdated)}: {nameof(sessionList)}: {sessionList.Count}");
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Log.Info($"{nameof(OnShutdown)}: {nameof(shutdownReason)}: {shutdownReason}");
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    }
}