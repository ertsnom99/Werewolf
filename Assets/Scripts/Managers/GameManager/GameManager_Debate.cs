using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Gameplay;

namespace Werewolf.Managers
{
	public partial class GameManager
	{
		private IEnumerator StartDebate(PlayerRef[] highlightedPlayers, int imageID, float duration)
		{
			RPC_SetPlayersCardHighlightVisible(highlightedPlayers, true);
#if UNITY_SERVER && UNITY_EDITOR
			SetPlayersCardHighlightVisible(highlightedPlayers, true);
#endif
			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected)
				{
					continue;
				}

				RPC_OnDebateStarted(playerInfo.Key, imageID, showConfirmButton: playerInfo.Value.IsAlive, duration);

				if (!playerInfo.Value.IsAlive)
				{
					continue;
				}

				WaitForPlayer(playerInfo.Key);
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(imageID, variables: null, countdownDuration: duration);
#endif
			float elapsedTime = .0f;

			while (PlayersWaitingFor.Count > 0 && elapsedTime < duration)
			{
				yield return 0;
				elapsedTime += Time.deltaTime;
			}
			
			RPC_SetPlayersCardHighlightVisible(highlightedPlayers, false);
#if UNITY_SERVER && UNITY_EDITOR
			SetPlayersCardHighlightVisible(highlightedPlayers, false);
#endif
			RPC_OnDebateEnded();
			PlayersWaitingFor.Clear();

#if UNITY_SERVER && UNITY_EDITOR
			OnDebateEnded();
#endif
			yield return new WaitForSeconds(Config.UITransitionNormalDuration);

			StartCoroutine(MoveToNextGameplayLoopStep());
		}

		private void OnPlayerSkipDebate()
		{
			_UIManager.TitleScreen.ConfirmClicked -= OnPlayerSkipDebate;

			_playerCards[Runner.LocalPlayer].DisplaySkip(true);

			RPC_SkipDebate();
		}

		private void OnDebateEnded()
		{
			_UIManager.TitleScreen.ConfirmClicked -= OnPlayerSkipDebate;

			foreach (KeyValuePair<PlayerRef, Card> card in _playerCards)
			{
				card.Value.DisplaySkip(false);
			}

			HideUI();
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_OnDebateStarted([RpcTarget] PlayerRef player, int imageID, bool showConfirmButton, float countdownDuration)
		{
			DisplayTitle(imageID, variables: null, showConfirmButton: showConfirmButton, countdownDuration: countdownDuration);

			if (!showConfirmButton)
			{
				return;
			}

			_UIManager.TitleScreen.ConfirmClicked += OnPlayerSkipDebate;
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_SkipDebate(RpcInfo info = default)
		{
			StopWaintingForPlayer(info.Source);
#if UNITY_SERVER && UNITY_EDITOR
			_playerCards[info.Source].DisplaySkip(true);
#endif
			RPC_PlayerSkippedDebate(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_PlayerSkippedDebate(PlayerRef player)
		{
			_playerCards[player].DisplaySkip(true);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_OnDebateEnded()
		{
			OnDebateEnded();
		}
		#endregion
	}
}