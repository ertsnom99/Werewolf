using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Gameplay;

namespace Werewolf.Managers
{
	public partial class GameManager
	{
		private readonly Dictionary<int, Marker> _markers = new();

		private GameObject InstantiateMarker(int markerID, Vector3 position)
		{
			if (_markers.ContainsKey(markerID))
			{
				Debug.LogError($"The marker {markerID} already exist. Multiple similar markers are not supported");
				return null;
			}
			
			if (!_gameplayDataManager.TryGetGameplayData(markerID, out MarkerData markerData))
			{
				Debug.LogError($"Could not find the marker {markerID}");
				return null;
			}

			Marker marker = Instantiate(GameConfig.MarkerPrefab, position, Quaternion.identity);
			_markers.Add(markerID, marker);

			marker.SetMarkerData(markerData);
			marker.DissolveIn();

			return marker.gameObject;
		}

		private void DestroyMarker(int markerID)
		{
			if (!_markers.ContainsKey(markerID))
			{
				return;
			}

			Marker marker = _markers[markerID];
			marker.DissolveFinished += OnDissolveFinished;
			marker.DissolveOut();
		}

		private void OnDissolveFinished(Marker marker)
		{
			marker.DissolveFinished -= OnDissolveFinished;

			_markers.Remove(marker.MarkerData.ID.HashCode);
			Destroy(marker.gameObject);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_InstantiateMarker(int markerID, Vector3 position)
		{
			InstantiateMarker(markerID, position);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_InstantiateMarker(int markerID, PlayerRef playerRelativeTo, Vector3 offset)
		{
			InstantiateMarker(markerID, _playerCards[playerRelativeTo].transform.position + offset);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DestroyMarker(int markerID)
		{
			DestroyMarker(markerID);
		}
		#endregion
	}
}