using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf.Network
{
	[Serializable]
	public struct PlayerInfo : INetworkStruct
	{
		public PlayerRef PlayerRef;
		[Networked, Capacity(24)]
		public string Nickname { get => default; set { } }
		public bool IsLeader;
		public bool IsInGame;
	}

	[Serializable]
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
	}

	public class NetworkDataManager : NetworkBehaviourSingleton<NetworkDataManager>, INetworkRunnerCallbacks
	{
		[Networked, Capacity(GameConfig.MAX_PLAYER_COUNT)]
		public NetworkDictionary<PlayerRef, PlayerInfo> PlayerInfos { get; }

		[field: SerializeField]
		public RolesSetup RolesSetup { get; private set; }

		[Networked]
		public bool RolesSetupReady { get; set; }

		private ChangeDetector _changeDetector;

		public event Action OnPlayerInfosChanged;

		public event Action OnInvalidRolesSetupReceived;

		public event Action OnRolesSetupReadyChanged;

		public static event Action OnSpawned;

		protected override void Awake()
		{
			base.Awake();
			DontDestroyOnLoad(gameObject);
		}

		public override void Spawned()
		{
			_changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

			OnSpawned?.Invoke();
		}

		public override void Render()
		{
			foreach (string change in _changeDetector.DetectChanges(this))
			{
				switch (change)
				{
					case nameof(PlayerInfos):
						OnPlayerInfosChanged?.Invoke();
						break;
					case nameof(RolesSetupReady):
						OnRolesSetupReadyChanged?.Invoke();
						break;
				}
			}
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_SetPlayerNickname(PlayerRef playerRef, string nickname)
		{
			PlayerInfo playerData = new();
			playerData.PlayerRef = playerRef;
			playerData.Nickname = nickname;
			playerData.IsLeader = PlayerInfos.Count <= 0;
			playerData.IsInGame = false;

			PlayerInfos.Set(playerRef, playerData);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_SetRolesSetup(RolesSetup rolesSetup, int minPlayerCount, RpcInfo info = default)
		{
			if (!PlayerInfos.ContainsKey(info.Source) || !PlayerInfos.Get(info.Source).IsLeader
				|| RolesSetupReady
				|| PlayerInfos.Count < minPlayerCount)
			{
				return;
			}

			if (!IsSetupValid(rolesSetup, minPlayerCount))
			{
				RPC_WarnInvalidRolesSetup(info.Source);
				return;
			}

			RolesSetup = rolesSetup;
			RolesSetupReady = true;
		}

		public void SetPlayersInGame()
		{
			foreach(KeyValuePair<PlayerRef, PlayerInfo> playerInfos in PlayerInfos)
			{
				PlayerInfo playerData = new();
				playerData.PlayerRef = playerInfos.Value.PlayerRef;
				playerData.Nickname = playerInfos.Value.Nickname;
				playerData.IsLeader = playerInfos.Value.IsLeader;
				playerData.IsInGame = true;

				PlayerInfos.Set(playerInfos.Key, playerData);
			}
		}

		private bool IsSetupValid(RolesSetup rolesSetup, int minPlayerCount)
		{
			//TODO: Check if setup is valid
			return true;
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_WarnInvalidRolesSetup([RpcTarget] PlayerRef player)
		{
			OnInvalidRolesSetupReceived?.Invoke();
		}

		#region Convertion Methods
		public static RolesSetup ConvertToRolesSetup(GameSetupData gameSetupData)
		{
			RolesSetup rolesSetup = new()
			{
				DefaultRole = gameSetupData.DefaultRole.GameplayTag.CompactTagId,
			};

			for (int i = 0; i < gameSetupData.MandatoryRoles.Length; i++)
			{
				rolesSetup.MandatoryRoles.Set(i, ConvertToRoleSetup(gameSetupData.MandatoryRoles[i]));
			}

			for (int i = 0; i < gameSetupData.AvailableRoles.Length; i++)
			{
				rolesSetup.AvailableRoles.Set(i, ConvertToRoleSetup(gameSetupData.AvailableRoles[i]));
			}

			return rolesSetup;
		}

		public static RoleSetup ConvertToRoleSetup(RoleSetupData roleSetupData)
		{
			RoleSetup roleSetup = new();

			for (int i = 0; i < roleSetupData.Pool.Length; i++)
			{
				roleSetup.Pool.Set(i, roleSetupData.Pool[i].GameplayTag.CompactTagId);
			}

			roleSetup.UseCount = roleSetupData.UseCount;

			return roleSetup;
		}

		public static void ConvertToRoleSetupDatas(NetworkArray<RoleSetup> roleSetups, out List<RoleSetupData> roleSetupDatas)
		{
			GameplayDatabaseManager _gameplayDatabaseManager = GameplayDatabaseManager.Instance;

			roleSetupDatas = new();

			foreach (RoleSetup roleSetup in roleSetups)
			{
				if (roleSetup.UseCount <= 0)
				{
					return;
				}

				List<RoleData> Pool = new();

				foreach (int role in roleSetup.Pool)
				{
					if (role <= 0)
					{
						continue;
					}

					Pool.Add(_gameplayDatabaseManager.GetGameplayData<RoleData>(role));
				}

				roleSetupDatas.Add(new()
				{
					Pool = Pool.ToArray(),
					UseCount = roleSetup.UseCount
				});
			}
		}
		#endregion

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

			foreach (KeyValuePair<PlayerRef, PlayerInfo> playerInfo in PlayerInfos)
			{
				PlayerInfo newPlayerData = new();
				newPlayerData.PlayerRef = playerInfo.Value.PlayerRef;
				newPlayerData.Nickname = playerInfo.Value.Nickname;
				newPlayerData.IsLeader = true;

				PlayerInfos.Set(playerInfo.Key, newPlayerData);

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