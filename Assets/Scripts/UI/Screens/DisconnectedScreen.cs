using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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
		private TMP_Text _disconnectedText;

		private readonly Dictionary<Transform, float> DisconnectedTextDisplayStartTimes = new();

		private GameConfig _config;

		private NetworkDataManager _networkDataManager;

		public void SetConfig(GameConfig config)
		{
			_config = config;
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
				if(Time.time - playerNicknameDisplayStartTime.Value > _config.DisconnectedTextDuration)
				{
					playerNicknamesToRemove.Add(playerNicknameDisplayStartTime.Key);
				}
			}

			foreach(Transform playerNickname in playerNicknamesToRemove)
			{
				Destroy(playerNickname.gameObject);
				DisconnectedTextDisplayStartTimes.Remove(playerNickname);
			}
		}

		public void DisplayDisconnectedPlayer(PlayerRef player)
		{
			TMP_Text playerNickname = Instantiate(_disconnectedText, _disconnectedTextsContainer);
			playerNickname.text = $"{_networkDataManager.PlayerInfos[player].Nickname} disconnected";

			DisconnectedTextDisplayStartTimes.Add(playerNickname.transform, Time.time);
		}
	}
}