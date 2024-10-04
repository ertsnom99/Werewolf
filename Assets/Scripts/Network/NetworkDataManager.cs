using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network.Configs;

namespace Werewolf.Network
{
	[Serializable]
	public struct PlayerNetworkInfo : INetworkStruct
	{
		public PlayerRef PlayerRef;
		[Networked, Capacity(24)]
		public string Nickname { get => default; set { } }
		public bool IsLeader;
		public bool IsConnected;
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
		public NetworkDictionary<PlayerRef, PlayerNetworkInfo> PlayerInfos { get; }

		[field: SerializeField]
		public RolesSetup RolesSetup { get; private set; }

		[Networked]
		public bool RolesSetupReady { get; private set; }

		private ChangeDetector _changeDetector;

		public static event Action FinishedSpawning;
		public event Action PlayerInfosChanged;
		public event Action InvalidRolesSetupReceived;
		public event Action RolesSetupReadyChanged;
		public event Action<PlayerRef> PlayerDisconnected;

		protected override void Awake()
		{
			base.Awake();
			DontDestroyOnLoad(gameObject);
		}

		public override void Spawned()
		{
			_changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

			FinishedSpawning?.Invoke();
		}

		public override void Render()
		{
			foreach (string change in _changeDetector.DetectChanges(this))
			{
				switch (change)
				{
					case nameof(PlayerInfos):
						PlayerInfosChanged?.Invoke();
						break;
					case nameof(RolesSetupReady):
						RolesSetupReadyChanged?.Invoke();
						break;
				}
			}
		}

		private bool IsSetupValid(RolesSetup rolesSetup, int minPlayerCount)
		{
			//TODO: Check if setup is valid
			return true;
		}

		public void ClearRolesSetup()
		{
			RolesSetup = new();
			RolesSetupReady = false;
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

		#region RPC Calls
		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_SetPlayerNickname(PlayerRef playerRef, string nickname)
		{
			PlayerNetworkInfo playerData = new()
			{
				PlayerRef = playerRef,
				Nickname = nickname,
				IsLeader = PlayerInfos.ContainsKey(playerRef) ? PlayerInfos[playerRef].IsLeader : PlayerInfos.Count <= 0,
				IsConnected = true
			};

			PlayerInfos.Set(playerRef, playerData);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_KickPlayer(PlayerRef kickedPlayer, RpcInfo info = default)
		{
			if (!PlayerInfos.ContainsKey(info.Source) || !PlayerInfos.Get(info.Source).IsLeader)
			{
				return;
			}

			Runner.Disconnect(kickedPlayer);
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

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_WarnInvalidRolesSetup([RpcTarget] PlayerRef player)
		{
			InvalidRolesSetupReceived?.Invoke();
		}
		#endregion

		void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
		{
			if (!HasStateAuthority || !PlayerInfos.ContainsKey(player))
			{
				return;
			}

			switch (runner.SceneManager.MainRunnerScene.buildIndex)
			{
				case (int)SceneDefs.MENU:
					RemovePlayerInfos(player);
					break;
				case (int)SceneDefs.GAME:
					SetPlayerDisconnected(player);
					break;
			}

			PlayerDisconnected?.Invoke(player);
		}

		private void RemovePlayerInfos(PlayerRef player)
		{
			bool removingLeader = PlayerInfos.Get(player).IsLeader;

			PlayerInfos.Remove(player);

			if (!removingLeader)
			{
				return;
			}

			foreach (KeyValuePair<PlayerRef, PlayerNetworkInfo> playerInfo in PlayerInfos)
			{
				PlayerNetworkInfo newPlayerData = playerInfo.Value;
				newPlayerData.IsLeader = true;

				PlayerInfos.Set(playerInfo.Key, newPlayerData);

				break;
			}
		}

		private void SetPlayerDisconnected(PlayerRef player)
		{
			PlayerNetworkInfo newPlayerData = PlayerInfos[player];
			newPlayerData.IsConnected = false;

			PlayerInfos.Set(player, newPlayerData);
		}

		void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
		{
			if (runner.SceneManager.MainRunnerScene.buildIndex != (int)SceneDefs.MENU)
			{
				return;
			}

			KeyValuePair<PlayerRef, PlayerNetworkInfo>[] disconnectedPlayers = PlayerInfos.Where(kv => !kv.Value.IsConnected).ToArray();

			bool isLeaderGone = false;

			foreach (KeyValuePair<PlayerRef, PlayerNetworkInfo> disconnectedPlayer in disconnectedPlayers)
			{
				if (PlayerInfos[disconnectedPlayer.Key].IsLeader)
				{
					isLeaderGone = true;
				}

				PlayerInfos.Remove(disconnectedPlayer.Key);
			}

			if (!isLeaderGone)
			{
				return;
			}

			foreach (KeyValuePair<PlayerRef, PlayerNetworkInfo> playerInfo in PlayerInfos)
			{
				PlayerNetworkInfo playerNetworkInfo = playerInfo.Value;
				playerNetworkInfo.IsLeader = true;

				PlayerInfos.Set(playerInfo.Key, playerNetworkInfo);
				break;
			}
		}

		#region Unused Callbacks
		void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

		void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }

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