using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;

namespace Werewolf.Network
{
    [Serializable]
    public struct PlayerInfo : INetworkStruct
    {
        public PlayerRef PlayerRef;
        [Networked, Capacity(24)]
        public string Nickname { get => default; set { } }
        public bool IsLeader;
    }

    /*[Serializable]
    public struct RoleSetup : INetworkStruct
    {
        [Networked, Capacity(5)]
        public NetworkArray<int> Pool { get; }
        public int UseCount;
    }

    [Serializable]
    public struct RolesSetup : INetworkStruct
    {
        public int DefaultRole;
        [Networked, Capacity(100)]
        public NetworkArray<RoleSetup> MandatoryRoles { get; }
        [Networked, Capacity(100)]
        public NetworkArray<RoleSetup> AvailableRoles { get; }
        public int MinPlayerCount;
    }*/

    public class GameDataManager : NetworkBehaviour, INetworkRunnerCallbacks
    {
        [Networked, Capacity(LaunchManager.MAX_PLAYER_COUNT)]
        public NetworkDictionary<PlayerRef, PlayerInfo> PlayerInfos { get; }

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
                    case nameof(PlayerInfos):
                        if (OnPlayerNicknamesChanged != null)
                        {
                            OnPlayerNicknamesChanged();
                        }
                        break;
                }
            }
        }
        // TODO: Test with RpcSources.Proxies
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        public void RPC_SetPlayerNickname(PlayerRef playerRef, string nickname)
        {
            PlayerInfo playerData = new PlayerInfo();
            playerData.PlayerRef = playerRef;
            playerData.Nickname = nickname;
            playerData.IsLeader = PlayerInfos.Count <= 0;

            PlayerInfos.Set(playerRef, playerData);
        }

        [Rpc(sources: RpcSources.All, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        public void RPC_SetRolesSetup()
        {
            // TODO: Store game setup
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!HasStateAuthority || !PlayerInfos.ContainsKey(player))
            {
                return;
            }

            bool removingFirstPlayer = PlayerInfos.Get(player).IsLeader;

            PlayerInfos.Remove(player);

            if (!removingFirstPlayer)
            {
                return;
            }

            foreach (KeyValuePair<PlayerRef, PlayerInfo> playerData in PlayerInfos)
            {
                PlayerInfo newPlayerData = new PlayerInfo();
                newPlayerData.PlayerRef = playerData.Value.PlayerRef;
                newPlayerData.Nickname = playerData.Value.Nickname;
                newPlayerData.IsLeader = true;

                PlayerInfos.Set(playerData.Key, newPlayerData);

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