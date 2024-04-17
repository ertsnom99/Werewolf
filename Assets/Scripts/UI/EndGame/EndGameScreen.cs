using System.Collections;
using TMPro;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Network;

namespace Werewolf.UI
{
	public class EndGameScreen : FadingScreen
	{
		[Header("UI")]
		[SerializeField]
		private GameObject _winnersTitle;

		[SerializeField]
		private RectTransform _winners;

		[SerializeField]
		private RectTransform _losers;

		[SerializeField]
		private EndGamePlayer _endGamePlayerPrefab;

		[SerializeField]
		private TMP_Text _countdownText;

		private GameConfig _config;

		private NetworkDataManager _networkDataManager;
		private GameplayDatabaseManager _gameplayDatabaseManager;

		private void Start()
		{
			_networkDataManager = NetworkDataManager.Instance;
			_gameplayDatabaseManager = GameplayDatabaseManager.Instance;
		}

		public void SetConfig(GameConfig config)
		{
			_config = config;
		}

		public void Initialize(EndGamePlayerInfo[] endGamePlayerInfos, float returnToLobbyCountdownDuration)
		{
			foreach(EndGamePlayerInfo endGamePlayerInfo in endGamePlayerInfos)
			{
				RectTransform parent;

				if (endGamePlayerInfo.Won)
				{
					parent = _winners;
				}
				else
				{
					parent = _losers;
				}

				EndGamePlayer endGamePlayer = Instantiate(_endGamePlayerPrefab, parent);

				RoleData roleData = _gameplayDatabaseManager.GetGameplayData<RoleData>(endGamePlayerInfo.Role);

				if (roleData)
				{
					endGamePlayer.Initialize(roleData.Image, _networkDataManager.PlayerInfos[endGamePlayerInfo.Player].Nickname, roleData.Name);
				}
				else
				{
					endGamePlayer.Initialize(_networkDataManager.PlayerInfos[endGamePlayerInfo.Player].Nickname);
				}
			}

			bool anyWinners = _winners.childCount > 0;
			_winnersTitle.SetActive(anyWinners);
			_winners.gameObject.SetActive(anyWinners);

			StartCoroutine(StartReturnToLobbyCountdown(returnToLobbyCountdownDuration));
		}

		private IEnumerator StartReturnToLobbyCountdown(float returnToLobbyCountdownDuration)
		{
			float currentCountdown = returnToLobbyCountdownDuration;

			while (currentCountdown > 0)
			{
				_countdownText.text = string.Format(_config.ReturnToLobbyCountdownText, Mathf.CeilToInt(currentCountdown));
				yield return 0;
				currentCountdown = Mathf.Max(currentCountdown - Time.deltaTime, .0f);
			}
		}

		protected override void OnFadeStarts(float targetOpacity) { }
	}
}