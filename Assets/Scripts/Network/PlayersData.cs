using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;

namespace Werewolf.Network
{
    [Serializable]
    public struct PlayerData : INetworkStruct
    {
        public PlayerRef PlayerRef;
        [Networked, Capacity(24)]
        public string Nickname { get => default; set { } }
        public bool IsLeader;
    }

    public class PlayersData : NetworkBehaviour, INetworkRunnerCallbacks
    {
        [Networked, Capacity(LaunchManager.MAX_PLAYER_COUNT)]
        public NetworkDictionary<PlayerRef, PlayerData> PlayerDatas { get; }

        private ChangeDetector _changeDetector;

        public event Action OnPlayerNicknamesChanged;

        public static event Action OnSpawned;

        public override void Spawned()
        {
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

            if (OnSpawned != null)
            {
                OnSpawned();
            }
        }

        public override void Render()
        {
            foreach (var change in _changeDetector.DetectChanges(this))
            {
                switch (change)
                {
                    case nameof(PlayerDatas):
                        if (OnPlayerNicknamesChanged != null)
                        {
                            OnPlayerNicknamesChanged();
                        }
                        break;
                }
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        public void RPC_SetPlayerNickname(PlayerRef playerRef, string nickname)
        {
            PlayerData playerData = new PlayerData();
            playerData.PlayerRef = playerRef;
            playerData.Nickname = nickname;
            playerData.IsLeader = PlayerDatas.Count <= 0;

            PlayerDatas.Set(playerRef, playerData);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!HasStateAuthority || !PlayerDatas.ContainsKey(player))
            {
                return;
            }

            bool removingFirstPlayer = PlayerDatas.Get(player).IsLeader;

            PlayerDatas.Remove(player);

            if (!removingFirstPlayer)
            {
                return;
            }

            foreach (KeyValuePair<PlayerRef, PlayerData> playerData in PlayerDatas)
            {
                PlayerData newPlayerData = new PlayerData();
                newPlayerData.PlayerRef = playerData.Value.PlayerRef;
                newPlayerData.Nickname = playerData.Value.Nickname;
                newPlayerData.IsLeader = true;

                PlayerDatas.Set(playerData.Key, newPlayerData);

                break;
            }
        }

        #region Unused Callbacks
        public void OnSceneLoadDone(NetworkRunner runner) { }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

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