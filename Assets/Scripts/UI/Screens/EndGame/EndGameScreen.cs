using Fusion;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using Utilities.GameplayData;
using Werewolf.Data;
using Werewolf.Network;

namespace Werewolf.UI
{
	[Serializable]
	public struct PlayerEndGameInfo : INetworkStruct
	{
		public PlayerRef Player;
		public int RoleID;
		public bool IsRoleRevealed;
		public bool Won;
	}

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
		private LocalizeStringEvent _countdownText;

		private IntVariable _countdownVariable;

		private NetworkDataManager _networkDataManager;
		private GameplayDataManager _gameplayDataManager;

		protected override void Awake()
		{
			base.Awake();

			_countdownVariable = (IntVariable)_countdownText.StringReference["Time"];

			if (_countdownVariable == null)
			{
				Debug.LogError($"{nameof(_countdownText)} must have a local int variable named Time");
			}
		}

		private void Start()
		{
			_networkDataManager = NetworkDataManager.Instance;
			_gameplayDataManager = GameplayDataManager.Instance;
		}

		public void Initialize(PlayerEndGameInfo[] endGamePlayerInfos, float countdownDuration)
		{
			foreach (PlayerEndGameInfo endGamePlayerInfo in endGamePlayerInfos)
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

				if (endGamePlayerInfo.RoleID != -1)
				{
					if (_gameplayDataManager.TryGetGameplayData(endGamePlayerInfo.RoleID, out RoleData roleData))
					{
						endGamePlayer.Initialize(roleData.SmallImage, _networkDataManager.PlayerInfos[endGamePlayerInfo.Player].Nickname, roleData.NameSingular);
					}
					else
					{
						Debug.LogError($"Could not find the role {endGamePlayerInfo.RoleID}");
					}
				}
				else
				{
					endGamePlayer.Initialize(_networkDataManager.PlayerInfos[endGamePlayerInfo.Player].Nickname);
				}
			}

			bool anyWinners = _winners.childCount > 0;
			_winnersTitle.SetActive(anyWinners);
			_winners.gameObject.SetActive(anyWinners);

			StartCoroutine(Countdown(countdownDuration));
		}

		private IEnumerator Countdown(float countdownDuration)
		{
			float timeLeft = countdownDuration;

			while (timeLeft > 0)
			{
				yield return 0;

				timeLeft = Mathf.Max(timeLeft - Time.deltaTime, .0f);
				_countdownVariable.Value = Mathf.CeilToInt(timeLeft);
			}
		}

		protected override void OnFadeStarts(float targetOpacity) { }
	}
}