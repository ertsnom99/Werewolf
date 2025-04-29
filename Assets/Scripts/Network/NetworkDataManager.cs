using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Localization;
using Utilities.GameplayData;
using Werewolf.Data;
using Werewolf.Network.Configs;

namespace Werewolf.Network
{
	public struct NetworkPlayerInfo : INetworkStruct
	{
		public PlayerRef PlayerRef;
		[Networked, Capacity(GameConfig.MAX_NICKNAME_CHARACTER_COUNT)]
		public string Nickname { get { return default; } readonly set { } }
		public bool IsLeader;
		public bool IsConnected;
	}

	public struct NetworkRoleSetup : INetworkStruct
	{
		[Networked, Capacity(GameConfig.MAX_ROLE_SETUP_POOL_COUNT)]
		public NetworkArray<int> Pool { get; }
		public int UseCount;
	}

	[Serializable]
	public struct SerializableRoleSetups
	{
		public SerializableRoleSetup[] MandatoryRoles;
		public SerializableRoleSetup[] OptionalRoles;
	}

	[Serializable]
	public struct SerializableRoleSetup
	{
		public int[] Pool;
		public int UseCount;
	}

	public struct RoleSetup
	{
		public RoleData[] Pool;
		public int UseCount;
	}

	public enum GameSpeed
	{
		Slow = 0,
		Normal,
		Fast
	}

	public class NetworkDataManager : NetworkBehaviourSingleton<NetworkDataManager>, INetworkRunnerCallbacks
	{
		[Header("Config")]
		[SerializeField]
		private GameConfig _gameConfig;

		[Networked, Capacity(GameConfig.MAX_PLAYER_COUNT)]
		public NetworkDictionary<PlayerRef, NetworkPlayerInfo> PlayerInfos { get; }

		[Networked, Capacity(GameConfig.MAX_PLAYER_COUNT)]
		public NetworkArray<NetworkRoleSetup> MandatoryRoles { get; }

		[Networked, Capacity(GameConfig.MAX_ROLE_SETUP_COUNT)]
		public NetworkArray<NetworkRoleSetup> OptionalRoles { get; }

		[Networked]
		public GameSpeed GameSpeed { get; private set; }

		[Networked]
		public bool PlayIntro { get; private set; }

		public bool GameSetupReady { get; private set; }

		public static event Action FinishedSpawning;
		public event Action PlayerInfosChanged;
		public event Action RoleSetupsChanged;
		public event Action GameSpeedChanged;
		public event Action PlayIntroChanged;
		public event Action InvalidRolesSetupReceived;
		public event Action GameSetupReadyChanged;
		public event Action<PlayerRef> PlayerDisconnected;

		private ChangeDetector _changeDetector;

		private GameplayDataManager _gameplayDataManager;

		protected override void Awake()
		{
			base.Awake();
			DontDestroyOnLoad(gameObject);

			_gameplayDataManager = GameplayDataManager.Instance;
		}

		public override void Spawned()
		{
			_changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
			GameSpeed = GameSpeed.Normal;
			PlayIntro = true;

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
					case nameof(MandatoryRoles):
					case nameof(OptionalRoles):
						RoleSetupsChanged?.Invoke();
						break;
					case nameof(GameSpeed):
						GameSpeedChanged?.Invoke();
						break;
					case nameof(PlayIntro):
						PlayIntroChanged?.Invoke();
						break;
				}
			}
		}

		public void ResetRoles()
		{
			MandatoryRoles.Clear();
			OptionalRoles.Clear();
		}

		public bool AreNetworkRoleSetupsValid(List<LocalizedString> warnings)
		{
			warnings.Clear();
			int totalMandatoryCount = 0;
			bool hasMandatoryWerewolf = false;

			for (int i = 0; i < MandatoryRoles.Length; i++)
			{
				NetworkRoleSetup networkRoleSetup = MandatoryRoles[i];

				if (networkRoleSetup.UseCount == 0)
				{
					break;
				}

				IsNetworkRoleSetupValid(networkRoleSetup, true, warnings, ref hasMandatoryWerewolf);

				totalMandatoryCount += networkRoleSetup.UseCount;
			}

			if (!hasMandatoryWerewolf)
			{
				warnings.Add(_gameConfig.NeedOneWerewolfWarning);
			}

			if (totalMandatoryCount > PlayerInfos.Count)
			{
				warnings.Add(_gameConfig.TooManyMandatoryRolesWarning);
			}

			for (int i = 0; i < OptionalRoles.Length; i++)
			{
				NetworkRoleSetup networkRoleSetup = OptionalRoles[i];

				if (networkRoleSetup.UseCount == 0)
				{
					break;
				}

				IsNetworkRoleSetupValid(networkRoleSetup, false, warnings, ref hasMandatoryWerewolf);
			}

			return warnings.Count == 0;
		}

		private bool IsNetworkRoleSetupValid(NetworkRoleSetup networkRoleSetup, bool checkForMandatoryWerewolves, List<LocalizedString> warnings, ref bool hasMandatoryWerewolf)
		{
			bool isValid = true;
			int PoolCount = 0;
			int werewolvesCount = 0;

			foreach (int roleID in networkRoleSetup.Pool)
			{
				if (roleID == 0)
				{
					continue;
				}

				if (!_gameplayDataManager.TryGetGameplayData(roleID, out RoleData roleData))
				{
					Debug.LogError($"Could not find the role {roleID}");
					continue;
				}

				if (roleData.Behavior && !roleData.Behavior.IsRolesSetupValid(MandatoryRoles, OptionalRoles, _gameplayDataManager, warnings))
				{
					isValid = false;
				}

				if (checkForMandatoryWerewolves)
				{
					if (roleData.PrimaryType == PrimaryRoleType.Werewolf)
					{
						werewolvesCount++;
					}

					PoolCount++;
				}
			}

			if (checkForMandatoryWerewolves && PoolCount - werewolvesCount < networkRoleSetup.UseCount)
			{
				hasMandatoryWerewolf = true;
			}

			return isValid;
		}

		public void SetRolesSetup(SerializableRoleSetups serializableRoleSetups)
		{
			Runner.SendReliableDataToServer(new ReliableKey(), Serialize(serializableRoleSetups));
		}

		void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
		{
			if (GameSetupReady
			|| !PlayerInfos.TryGet(player, out NetworkPlayerInfo playerInfo)
			|| !playerInfo.IsLeader)
			{
				return;
			}

			SerializableRoleSetups serializableRoleSetups = Deserialize<SerializableRoleSetups>(data.Array);
			SerializableRoleSetup[] mandatoryRoles = serializableRoleSetups.MandatoryRoles;
			SerializableRoleSetup[] optionalRoles = serializableRoleSetups.OptionalRoles;

			if (mandatoryRoles.Length > GameConfig.MAX_PLAYER_COUNT || optionalRoles.Length > GameConfig.MAX_ROLE_SETUP_COUNT)
			{
				return;
			}

			MandatoryRoles.Clear();
			FillNetworkRoleSetups(mandatoryRoles, MandatoryRoles);

			OptionalRoles.Clear();
			FillNetworkRoleSetups(optionalRoles, OptionalRoles);

			static void FillNetworkRoleSetups(SerializableRoleSetup[] serializableRoleSetup, NetworkArray<NetworkRoleSetup> networkRoleSetups)
			{
				NetworkRoleSetup roleSetup = new();

				for (int i = 0; i < serializableRoleSetup.Length; i++)
				{
					roleSetup.Pool.Clear();

					for (int j = 0; j < serializableRoleSetup[i].Pool.Length; j++)
					{
						if (serializableRoleSetup[i].Pool[j] == 0)
						{
							break;
						}

						roleSetup.Pool.Set(j, serializableRoleSetup[i].Pool[j]);
					}

					roleSetup.UseCount = serializableRoleSetup[i].UseCount;
					networkRoleSetups.Set(i, roleSetup);
				}
			}
		}

		private byte[] Serialize<T>(T data) where T : struct
		{
			var formatter = new BinaryFormatter();
			var stream = new MemoryStream();
			formatter.Serialize(stream, data);
			return stream.ToArray();
		}

		public T Deserialize<T>(byte[] array) where T : struct
		{
			var stream = new MemoryStream(array);
			var formatter = new BinaryFormatter();
			return (T)formatter.Deserialize(stream);
		}

		public void ResetGameSetupReady()
		{
			SetGameSetupReady(false);
			RPC_SetGameSetupReady(false);
		}

		private void SetGameSetupReady(bool isReady)
		{
			GameSetupReady = isReady;
			GameSetupReadyChanged?.Invoke();
		}

		public List<RoleSetup> ConvertToRoleSetups(NetworkArray<NetworkRoleSetup> networkRoleSetups)
		{
			List<RoleSetup> roleSetups = new();

			foreach (NetworkRoleSetup networkRoleSetup in networkRoleSetups)
			{
				if (networkRoleSetup.UseCount <= 0)
				{
					return roleSetups;
				}

				List<RoleData> Pool = new();

				foreach (int roleID in networkRoleSetup.Pool)
				{
					if (roleID != 0)
					{
						if (_gameplayDataManager.TryGetGameplayData(roleID, out RoleData roleData))
						{
							Pool.Add(roleData);
						}
						else
						{
							Debug.LogError($"Could not find the role {roleID}");
						}
					}
				}

				roleSetups.Add(new()
				{
					Pool = Pool.ToArray(),
					UseCount = networkRoleSetup.UseCount
				});
			}

			return roleSetups;
		}

		void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
		{
			if (runner.SceneManager.MainRunnerScene.buildIndex != (int)SceneDefs.MENU)
			{
				return;
			}

			KeyValuePair<PlayerRef, NetworkPlayerInfo>[] disconnectedPlayers = PlayerInfos.Where(kv => !kv.Value.IsConnected).ToArray();

			bool isLeaderGone = false;

			foreach (KeyValuePair<PlayerRef, NetworkPlayerInfo> disconnectedPlayer in disconnectedPlayers)
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

			foreach (KeyValuePair<PlayerRef, NetworkPlayerInfo> playerInfo in PlayerInfos)
			{
				NetworkPlayerInfo networkPlayerInfo = playerInfo.Value;
				networkPlayerInfo.IsLeader = true;

				PlayerInfos.Set(playerInfo.Key, networkPlayerInfo);
				break;
			}
		}

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

			foreach (KeyValuePair<PlayerRef, NetworkPlayerInfo> playerInfo in PlayerInfos)
			{
				NetworkPlayerInfo newPlayerData = playerInfo.Value;
				newPlayerData.IsLeader = true;

				PlayerInfos.Set(playerInfo.Key, newPlayerData);

				break;
			}
		}

		private void SetPlayerDisconnected(PlayerRef player)
		{
			NetworkPlayerInfo newPlayerData = PlayerInfos[player];
			newPlayerData.IsConnected = false;

			PlayerInfos.Set(player, newPlayerData);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_PromotePlayer(PlayerRef promotedPlayer, RpcInfo info = default)
		{
			if (!GameSetupReady
			&& PlayerInfos.TryGet(info.Source, out NetworkPlayerInfo playerInfo)
			&& playerInfo.IsLeader
			&& PlayerInfos.TryGet(promotedPlayer, out NetworkPlayerInfo promotedPlayerInfo))
			{
				playerInfo.IsLeader = false;
				PlayerInfos.Set(info.Source, playerInfo);

				promotedPlayerInfo.IsLeader = true;
				PlayerInfos.Set(promotedPlayer, promotedPlayerInfo);
			}
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_KickPlayer(PlayerRef kickedPlayer, RpcInfo info = default)
		{
			if (!GameSetupReady
			&& PlayerInfos.TryGet(info.Source, out NetworkPlayerInfo playerInfo)
			&& playerInfo.IsLeader)
			{
				Runner.Disconnect(kickedPlayer);
			}
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_SetPlayerNickname(PlayerRef playerRef, string nickname)
		{
			if (GameSetupReady)
			{
				return;
			}

			NetworkPlayerInfo playerData = new()
			{
				PlayerRef = playerRef,
				Nickname = nickname,
				IsLeader = PlayerInfos.TryGet(playerRef, out NetworkPlayerInfo playerInfo) ? playerInfo.IsLeader : PlayerInfos.Count <= 0,
				IsConnected = true
			};

			PlayerInfos.Set(playerRef, playerData);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_SetGameSpeed(GameSpeed gameSpeed, RpcInfo info = default)
		{
			if (!GameSetupReady
			&& PlayerInfos.TryGet(info.Source, out NetworkPlayerInfo playerInfo)
			&& playerInfo.IsLeader)
			{
				GameSpeed = gameSpeed;
			}
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_SetPlayIntro(bool playIntro, RpcInfo info = default)
		{
			if (!GameSetupReady
			&& PlayerInfos.TryGet(info.Source, out NetworkPlayerInfo playerInfo)
			&& playerInfo.IsLeader)
			{
				PlayIntro = playIntro;
			}
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_SetGameSetupReady(RpcInfo info = default)
		{
			if (GameSetupReady
			|| PlayerInfos.Count < _gameConfig.MinPlayerCount
			|| !PlayerInfos.TryGet(info.Source, out NetworkPlayerInfo playerInfo)
			|| !playerInfo.IsLeader)
			{
				return;
			}

			if (!AreNetworkRoleSetupsValid(new()))
			{
				RPC_WarnInvalidRolesSetup(info.Source);
				return;
			}

			SetGameSetupReady(true);
			RPC_SetGameSetupReady(true);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_WarnInvalidRolesSetup([RpcTarget] PlayerRef player)
		{
			InvalidRolesSetupReceived?.Invoke();
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_SetGameSetupReady(bool isReady)
		{
			SetGameSetupReady(isReady);
		}
		#endregion

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

		void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }

		void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

		void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
		#endregion
	}
}