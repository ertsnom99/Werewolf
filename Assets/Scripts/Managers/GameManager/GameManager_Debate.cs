using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Werewolf
{
	public partial class GameManager
	{
		private IEnumerator StartDebate(int imageID, float duration)
		{
			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[playerInfo.Key].IsConnected)
				{
					continue;
				}

				RPC_OnDebateStarted(playerInfo.Key, imageID, duration, playerInfo.Value.IsAlive);

				if (!playerInfo.Value.IsAlive)
				{
					continue;
				}

				WaitForPlayer(playerInfo.Key);
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(imageID, duration, confirmButtonText: Config.SkipText);
#endif
			float elapsedTime = .0f;

			while (PlayersWaitingFor.Count > 0 && elapsedTime < duration)
			{
				yield return 0;
				elapsedTime += Time.deltaTime;
			}

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

			_playerCards[Runner.LocalPlayer].SetVotingStatusVisible(true);
			_playerCards[Runner.LocalPlayer].UpdateVotingStatus(false);

			RPC_SkipDebate();
		}

		private void OnDebateEnded()
		{
			_UIManager.TitleScreen.ConfirmClicked -= OnPlayerSkipDebate;

			foreach (KeyValuePair<PlayerRef, Card> card in _playerCards)
			{
				card.Value.SetVotingStatusVisible(false);
			}
			HideUI();
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_OnDebateStarted([RpcTarget] PlayerRef player, int imageID, float countdownDuration, bool showConfirmButton)
		{
			DisplayTitle(imageID, countdownDuration, showConfirmButton, Config.SkipText);

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
			_playerCards[info.Source].SetVotingStatusVisible(true);
			_playerCards[info.Source].UpdateVotingStatus(false);
#endif
			RPC_PlayerSkippedDebate(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_PlayerSkippedDebate(PlayerRef player)
		{
			_playerCards[player].SetVotingStatusVisible(true);
			_playerCards[player].UpdateVotingStatus(false);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_OnDebateEnded()
		{
			OnDebateEnded();
		}
		#endregion
	}
}