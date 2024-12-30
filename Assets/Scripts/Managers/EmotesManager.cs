using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Gameplay;
using Werewolf.Network;
using Werewolf.UI;

namespace Werewolf.Managers
{
	public class EmotesManager : NetworkBehaviourSingleton<EmotesManager>
	{
		[field: Header("Emotes")]
		[SerializeField]
		private Sprite[] _emotes;
		[SerializeField]
		private Emote _emotePrefab;
		[SerializeField]
		private float _maxDistance;
		[SerializeField]
		private Vector3 _globalOffset;

		[field: Header("Spam")]
		[SerializeField]
		private float _delay;
		[SerializeField]
		private int _limit;

		private bool _asleepCanSee;

		private Dictionary<PlayerRef, Card> _playerCards;
		private readonly Dictionary<PlayerRef, Usage> _playerUsage = new();
		
		private class Usage
		{
			public int amount;
			public float elapsedTime;
		}

		private EmoteScreen _emoteScreen;

		private GameManager _gameManager;

		public void Initialize(GameConfig config)
		{
			_emoteScreen = UIManager.Instance.EmoteScreen;

			_emoteScreen.SetEmotes(_emotes);
			_emoteScreen.EmoteSelected += OnEmoteSelected;
#if UNITY_SERVER
			_gameManager = GameManager.Instance;

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				_playerUsage.Add(playerInfo.Key, new() { amount = 0, elapsedTime = 0 });
			}
#endif
		}

		public void SetPlayerCards(Dictionary<PlayerRef, Card> playerCards)
		{
			_playerCards = playerCards;

			foreach (KeyValuePair<PlayerRef, Card> playerCard in playerCards)
			{
				playerCard.Value.LeftClickHolded += card => { _emoteScreen.ShowEmoteSelection(card.Player, card.transform.position); };
			}
		}
#if UNITY_SERVER
		private void Update()
		{
			foreach (KeyValuePair<PlayerRef, Usage> usage in _playerUsage)
			{
				if (usage.Value.amount <= 0)
				{
					continue;
				}

				if (usage.Value.elapsedTime >= _delay)
				{
					usage.Value.amount--;
					usage.Value.elapsedTime -= _delay;

					if (usage.Value.amount <= 0)
					{
						usage.Value.elapsedTime = 0;
						continue;
					}
				}

				usage.Value.elapsedTime += Time.deltaTime;
			}
		}
#endif
		private void OnEmoteSelected(PlayerRef selectedPlayer, int emoteIndex)
		{
			RPC_ShowEmote(selectedPlayer, emoteIndex);
		}

		private void ShowEmote(PlayerRef selectedPlayer, int emoteIndex)
		{
			if (_emotes.Length <= emoteIndex)
			{
				Debug.LogError($"No emote is set for the index {emoteIndex}");
				return;
			}

			Vector3 positionOffsetRelativeToCard = Quaternion.Euler(0, Random.Range(.0f, 360.0f), 0) * Vector3.back * Random.Range(.0f, _maxDistance);

			Emote emote = Instantiate(_emotePrefab, _playerCards[selectedPlayer].OriginalPosition + positionOffsetRelativeToCard + _globalOffset, Quaternion.identity);
			emote.SetEmote(_emotes[emoteIndex]);
		}

		public void SetAsleepCanSee(bool asleepCanSee)
		{
			_asleepCanSee = asleepCanSee;
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_ShowEmote(PlayerRef selectedPlayer, int emoteIndex, RpcInfo info = default)
		{
			if (_playerUsage[info.Source].amount >= _limit || !_gameManager.IsPlayerAwake(info.Source))
			{
				return;
			}

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerGameInfo in _gameManager.PlayerGameInfos)
			{
				if (!_asleepCanSee && !playerGameInfo.Value.IsAwake)
				{
					continue;
				}

				RPC_ShowEmote(playerGameInfo.Key, selectedPlayer, emoteIndex);
			}

			_playerUsage[info.Source].amount++;
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_ShowEmote([RpcTarget] PlayerRef player, PlayerRef selectedPlayer, int emoteIndex)
		{
			ShowEmote(selectedPlayer, emoteIndex);
		}
		#endregion
	}
}
