using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using Werewolf.Data;
using Werewolf.Network;

namespace Werewolf.UI
{
	public class DisconnectedScreen : MonoBehaviour
	{
		[Header("UI")]
		[SerializeField]
		private Transform _disconnectedTextsContainer;

		[SerializeField]
		private LocalizeStringEvent _disconnectedText;

		private GameConfig _gameConfig;
		private readonly Dictionary<Transform, float> DisconnectedTextDisplayStartTimes = new();

		private NetworkDataManager _networkDataManager;

		public void SetConfig(GameConfig config)
		{
			_gameConfig = config;
		}

		private void Start()
		{
			_networkDataManager = NetworkDataManager.Instance;
		}

		private void Update()
		{
			List<Transform> playerNicknamesToRemove = new();

			foreach (KeyValuePair<Transform, float> playerNicknameDisplayStartTime in DisconnectedTextDisplayStartTimes)
			{
				if(Time.time - playerNicknameDisplayStartTime.Value > _gameConfig.DisconnectedTextDuration)
				{
					playerNicknamesToRemove.Add(playerNicknameDisplayStartTime.Key);
				}
			}

			foreach (Transform playerNickname in playerNicknamesToRemove)
			{
				Destroy(playerNickname.gameObject);
				DisconnectedTextDisplayStartTimes.Remove(playerNickname);
			}
		}

		public void DisplayDisconnectedPlayer(PlayerRef player)
		{
			LocalizeStringEvent disconnectedText = Instantiate(_disconnectedText, _disconnectedTextsContainer);
			((StringVariable)disconnectedText.StringReference["Nickname"]).Value = _networkDataManager.PlayerInfos[player].Nickname;

			DisconnectedTextDisplayStartTimes.Add(disconnectedText.transform, Time.time);
		}
	}
}