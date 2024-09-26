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

		void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnConnectedToServer)}: {nameof(runner.CurrentConnectionType)}: {runner.CurrentConnectionType}, {nameof(runner.LocalPlayer)}: {runner.LocalPlayer}");
		}

		void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnConnectFailed)}: {nameof(remoteAddress)}: {remoteAddress}, {nameof(reason)}: {reason}");
		}

		void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnConnectRequest)}: {nameof(request.RemoteAddress)}: {request.RemoteAddress}");
		}

		void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnCustomAuthenticationResponse)}");
		}

		void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnDisconnectedFromServer)} - {reason}: {nameof(runner.LocalPlayer)}: {runner.LocalPlayer}");
		}

		void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnHostMigration)}: {nameof(HostMigrationToken)}: {hostMigrationToken}");
		}

		void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }

		void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

		void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

		void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

		void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnPlayerJoined)}: {nameof(player)}: {player}");
		}

		void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnPlayerLeft)}: {nameof(player)}: {player}");
		}

		void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

		void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnReliableDataReceived)}: {nameof(PlayerRef)}:{player}, {nameof(key)}:{key}, {nameof(data)}:{data.Count}");
		}

		void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnSceneLoadDone)}: {nameof(runner.SceneManager.MainRunnerScene)}: {runner.SceneManager.MainRunnerScene.name}");
		}

		void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnSceneLoadStart)}: {nameof(runner.SceneManager.MainRunnerScene)}: {runner.SceneManager.MainRunnerScene.name}");
		}

		void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnSessionListUpdated)}: {nameof(sessionList)}: {sessionList.Count}");
		}

		void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
		{
			Log.Info($"{nameof(INetworkRunnerCallbacks.OnShutdown)}: {nameof(shutdownReason)}: {shutdownReason}");
		}

		void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
	}
}