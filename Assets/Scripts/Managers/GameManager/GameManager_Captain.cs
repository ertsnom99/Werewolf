using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Werewolf.GameHistoryManager;

namespace Werewolf
{
	public partial class GameManager
	{
		private PlayerRef _captain;
		private GameObject _captainCard;

		private IEnumerator _chooseNextCaptainCoroutine;

		private bool _isNextCaptainChoiceCompleted;

		private readonly int CAPTAIN_VOTE_MODIFIER = 2;

		private void SetCaptain(PlayerRef captain)
		{
			_captain = captain;

			_gameHistoryManager.AddEntry(Config.CaptainChangedGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "Player",
												Data = _networkDataManager.PlayerInfos[_captain].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});
		}

		#region Choose Captain
		private IEnumerator ChooseNextCaptain()
		{
			List<PlayerRef> choices = new();

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (!playerInfo.Value.IsAlive || playerInfo.Key == _captain)
				{
					continue;
				}

				choices.Add(playerInfo.Key);
			}

			if (choices.Count <= 0)
			{
				RPC_DestroyCaptainCard();
#if UNITY_SERVER && UNITY_EDITOR
				Destroy(_captainCard);
#endif
				_chooseNextCaptainCoroutine = null;
				_isNextCaptainChoiceCompleted = true;
				yield break;
			}

			if (!ChoosePlayers(_captain,
								choices,
								Config.ChooseNextCaptainImage.CompactTagId,
								Config.CaptainChoiceDuration * GameSpeedModifier,
								true,
								1,
								ChoicePurpose.Other,
								OnChoosedNextCaptain))
			{
				if (choices.Count >= 1)
				{
					StartCoroutine(EndChoosingNextCaptain(choices[Random.Range(0, choices.Count)]));
				}

				yield break;
			}

			foreach (var player in PlayerGameInfos)
			{
				if (!_networkDataManager.PlayerInfos[player.Key].IsConnected || player.Key == _captain)
				{
					continue;
				}

				RPC_DisplayTitle(player.Key, Config.OldCaptainChoosingImage.CompactTagId);
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(Config.OldCaptainChoosingImage.CompactTagId);
#endif
			float elapsedTime = .0f;

			while (_networkDataManager.PlayerInfos[_captain].IsConnected && elapsedTime < Config.CaptainChoiceDuration * GameSpeedModifier)
			{
				yield return 0;
				elapsedTime += Time.deltaTime;
			}

			StartCoroutine(EndChoosingNextCaptain(choices[Random.Range(0, choices.Count)]));
		}

		private void OnChoosedNextCaptain(PlayerRef[] nextCaptain)
		{
			if (_chooseNextCaptainCoroutine == null)
			{
				return;
			}

			StartCoroutine(EndChoosingNextCaptain(nextCaptain[0]));
		}

		private IEnumerator EndChoosingNextCaptain(PlayerRef nextCaptain)
		{
			StopCoroutine(_chooseNextCaptainCoroutine);
			_chooseNextCaptainCoroutine = null;

			StopChoosingPlayers(_captain);

			RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			HideUI();
#endif
			yield return new WaitForSeconds(Config.UITransitionNormalDuration);

			SetCaptain(nextCaptain);

			yield return ShowCaptain(false);

			_isNextCaptainChoiceCompleted = true;
		}

		private IEnumerator ShowCaptain(bool createCard)
		{
			if (createCard)
			{
				RPC_InstantiateCaptainCard(_captain);
#if UNITY_SERVER && UNITY_EDITOR
				_captainCard = Instantiate(Config.CaptainCardPrefab, _playerCards[_captain].transform.position + Config.CaptainCardOffset, Quaternion.identity);
#endif
			}
			else
			{
				RPC_MoveCaptainCard(_captain);
#if UNITY_SERVER && UNITY_EDITOR
				StartCoroutine(MoveCaptainCard(_playerCards[_captain].transform.position + Config.CaptainCardOffset));
#endif
			}

			float titleDuration = Config.CaptainRevealDuration * GameSpeedModifier;
			StartCoroutine(HighlightPlayerToggle(_captain, titleDuration));
			yield return DisplayTitleForAllPlayers(Config.CaptainRevealImage.CompactTagId, titleDuration);
		}

		private IEnumerator MoveCaptainCard(Vector3 newPosition)
		{
			Vector3 startingPosition = _captainCard.transform.position;
			float elapsedTime = .0f;

			while (elapsedTime < Config.CaptainCardMovementDuration)
			{
				yield return 0;

				elapsedTime += Time.deltaTime;
				float progress = elapsedTime / Config.CaptainCardMovementDuration;

				_captainCard.transform.position = Vector3.Lerp(startingPosition, newPosition, Config.CaptainCardMovementXY.Evaluate(progress))
				+ Vector3.up * Config.CaptainCardMovementYOffset.Evaluate(progress);
			}
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_InstantiateCaptainCard(PlayerRef captain)
		{
			_captainCard = Instantiate(Config.CaptainCardPrefab, _playerCards[captain].transform.position + Config.CaptainCardOffset, Quaternion.identity);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_MoveCaptainCard(PlayerRef captain)
		{
			StartCoroutine(MoveCaptainCard(_playerCards[captain].transform.position + Config.CaptainCardOffset));
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DestroyCaptainCard()
		{
			Destroy(_captainCard);
		}
		#endregion
		#endregion
	}
}