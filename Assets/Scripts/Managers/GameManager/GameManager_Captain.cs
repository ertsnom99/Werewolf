using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Gameplay;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Managers
{
	public partial class GameManager
	{
		private PlayerRef _captain;
		private GameObject _captainCard;

		private IEnumerator _chooseNextCaptainCoroutine;
		private List<PlayerRef> _captainChoices = new();
		private bool _isNextCaptainChoiceCompleted;

		private readonly int CAPTAIN_VOTE_MODIFIER = 2;

		private void SetCaptain(PlayerRef captain)
		{
			_captain = captain;

			_gameHistoryManager.AddEntry(Config.CaptainChangedGameHistoryEntry.ID,
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
			_captainChoices = GetAlivePlayers();
			_captainChoices.Remove(_captain);

			if (_captainChoices.Count <= 0)
			{
				RPC_DestroyCaptainCard();
#if UNITY_SERVER && UNITY_EDITOR
				Destroy(_captainCard);
#endif
				_chooseNextCaptainCoroutine = null;
				_isNextCaptainChoiceCompleted = true;
				yield break;
			}

			if (!SelectPlayers(_captain,
								_captainChoices,
								Config.ChooseNextCaptainTitle.ID.HashCode,
								Config.CaptainChoiceDuration * GameSpeedModifier,
								true,
								1,
								ChoicePurpose.Other,
								OnChoosedNextCaptain))
			{
				if (_captainChoices.Count >= 1)
				{
					ChooseRandomCaptain(_captainChoices);
				}

				yield break;
			}

			foreach (var player in PlayerGameInfos)
			{
				if (_networkDataManager.PlayerInfos[player.Key].IsConnected && player.Key != _captain)
				{
					RPC_DisplayTitle(player.Key, Config.OldCaptainChoosingTitle.ID.HashCode);
				}
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(Config.OldCaptainChoosingTitle.ID.HashCode);
#endif
			float elapsedTime = .0f;

			while (_networkDataManager.PlayerInfos[_captain].IsConnected && elapsedTime < Config.CaptainChoiceDuration * GameSpeedModifier)
			{
				yield return 0;
				elapsedTime += Time.deltaTime;
			}

			ChooseRandomCaptain(_captainChoices);
		}

		private void OnChoosedNextCaptain(PlayerRef[] nextCaptain)
		{
			if (_chooseNextCaptainCoroutine == null)
			{
				return;
			}

			if (nextCaptain == null || nextCaptain.Length <= 0)
			{
				ChooseRandomCaptain(_captainChoices);
				return;
			}

			StartCoroutine(EndChoosingNextCaptain(nextCaptain[0]));
		}

		private void ChooseRandomCaptain(List<PlayerRef> choices)
		{
			StartCoroutine(EndChoosingNextCaptain(choices[Random.Range(0, choices.Count)]));
		}

		private IEnumerator EndChoosingNextCaptain(PlayerRef nextCaptain)
		{
			StopCoroutine(_chooseNextCaptainCoroutine);
			_chooseNextCaptainCoroutine = null;

			StopSelectingPlayers(_captain);

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
			yield return DisplayTitleForAllPlayers(Config.CaptainRevealTitle.ID.HashCode, titleDuration);
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