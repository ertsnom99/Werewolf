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
		private GameObject _captainMarker;
		private IEnumerator _chooseNextCaptainCoroutine;
		private List<PlayerRef> _captainChoices = new();
		private bool _isNextCaptainChoiceCompleted;

		private readonly int CAPTAIN_VOTE_MODIFIER = 2;

		private void SetCaptain(PlayerRef captain)
		{
			_captain = captain;

			_gameHistoryManager.AddEntry(GameConfig.CaptainChangedGameHistoryEntry.ID,
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
				RPC_DestroyCaptainMarker();
#if UNITY_SERVER && UNITY_EDITOR
				DestroyMarker(GameConfig.CaptainMarkerData.ID.HashCode);
				_captainMarker = null;
#endif
				_chooseNextCaptainCoroutine = null;
				_isNextCaptainChoiceCompleted = true;
				yield break;
			}

			if (!SelectPlayers(_captain,
								_captainChoices,
								GameConfig.ChooseNextCaptainTitleScreen.ID.HashCode,
								GameConfig.CaptainChoiceDuration * GameSpeedModifier,
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
					RPC_DisplayTitle(player.Key, GameConfig.OldCaptainChoosingTitleScreen.ID.HashCode);
				}
			}
#if UNITY_SERVER && UNITY_EDITOR
			DisplayTitle(GameConfig.OldCaptainChoosingTitleScreen.ID.HashCode);
#endif
			float elapsedTime = .0f;

			while (_networkDataManager.PlayerInfos[_captain].IsConnected && elapsedTime < GameConfig.CaptainChoiceDuration * GameSpeedModifier)
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
			yield return new WaitForSeconds(GameConfig.UITransitionNormalDuration);

			SetCaptain(nextCaptain);

			yield return ShowCaptain(false);

			_isNextCaptainChoiceCompleted = true;
		}

		private IEnumerator ShowCaptain(bool createMarker)
		{
			if (createMarker)
			{
				RPC_InstantiateCaptainMarker(_captain);
#if UNITY_SERVER && UNITY_EDITOR
				_captainMarker = InstantiateMarker(GameConfig.CaptainMarkerData.ID.HashCode, _playerCards[_captain].transform.position + GameConfig.CaptainMarkerOffset);
#endif
			}
			else
			{
				RPC_MoveCaptainMarker(_captain);
#if UNITY_SERVER && UNITY_EDITOR
				StartCoroutine(MoveCaptainMarker(_playerCards[_captain].transform.position + GameConfig.CaptainMarkerOffset));
#endif
			}

			float titleDuration = GameConfig.CaptainRevealDuration * GameSpeedModifier;
			StartCoroutine(HighlightPlayerToggle(_captain, titleDuration));
			yield return DisplayTitleForAllPlayers(GameConfig.CaptainRevealTitleScreen.ID.HashCode, titleDuration, playerNickname: _networkDataManager.PlayerInfos[_captain].Nickname);
		}

		private IEnumerator MoveCaptainMarker(Vector3 newPosition)
		{
			Vector3 startingPosition = _captainMarker.transform.position;
			float elapsedTime = .0f;

			while (elapsedTime < GameConfig.CaptainMarkerMovementDuration)
			{
				yield return 0;

				elapsedTime += Time.deltaTime;
				float progress = elapsedTime / GameConfig.CaptainMarkerMovementDuration;

				_captainMarker.transform.position = Vector3.Lerp(startingPosition, newPosition, GameConfig.CaptainMarkerMovementXY.Evaluate(progress))
				+ Vector3.up * GameConfig.CaptainMarkerMovementYOffset.Evaluate(progress);
			}
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_InstantiateCaptainMarker(PlayerRef captain)
		{
			_captainMarker = InstantiateMarker(GameConfig.CaptainMarkerData.ID.HashCode, _playerCards[captain].transform.position + GameConfig.CaptainMarkerOffset);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_MoveCaptainMarker(PlayerRef captain)
		{
			StartCoroutine(MoveCaptainMarker(_playerCards[captain].transform.position + GameConfig.CaptainMarkerOffset));
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DestroyCaptainMarker()
		{
			DestroyMarker(GameConfig.CaptainMarkerData.ID.HashCode);
			_captainMarker = null;
		}
		#endregion
		#endregion
	}
}