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
		private GameConfig _gameConfig;
		private EmoteScreen _emoteScreen;
		private bool _asleepCanSee;
		private Dictionary<PlayerRef, Card> _playerCards;
		private readonly Dictionary<PlayerRef, Usage> _playerUsage = new();
		
		private class Usage
		{
			public int amount;
			public float elapsedTime;
		}

		private bool _showEmoteSelectionEnabled;

		private GameManager _gameManager;

		public void Initialize(GameConfig config)
		{
			_gameConfig = config;
			_emoteScreen = UIManager.Instance.EmoteScreen;

			_emoteScreen.SetEmotes(config.Emotes);
			_emoteScreen.EmoteSelected += OnEmoteSelected;

			_showEmoteSelectionEnabled = true;
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
				playerCard.Value.LeftClickHolded += OnLeftClickHolded;
			}
		}

		private void OnLeftClickHolded(Card card)
		{
			if (_showEmoteSelectionEnabled)
			{
				_emoteScreen.ShowEmoteSelection(card.Player, card.transform.position);
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

				if (usage.Value.elapsedTime >= _gameConfig.EmoteDelay)
				{
					usage.Value.amount--;
					usage.Value.elapsedTime -= _gameConfig.EmoteDelay;

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
		public void EnableShowEmoteSelection(bool enable)
		{
			_showEmoteSelectionEnabled = enable;
		}

		private void OnEmoteSelected(PlayerRef selectedPlayer, int emoteIndex)
		{
			RPC_ShowEmote(selectedPlayer, emoteIndex);
		}

		private void ShowEmote(PlayerRef selectedPlayer, int emoteIndex)
		{
			if (_gameConfig.Emotes.Length <= emoteIndex)
			{
				Debug.LogError($"No emote is set for the index {emoteIndex}");
				return;
			}

			Vector3 positionOffsetRelativeToCard = Quaternion.Euler(0, Random.Range(.0f, 360.0f), 0) * Vector3.back * Random.Range(.0f, _gameConfig.EmoteMaxDistance);

			Emote emote = Instantiate(_gameConfig.EmotePrefab, _playerCards[selectedPlayer].OriginalPosition + positionOffsetRelativeToCard + _gameConfig.EmoteGlobalOffset, Quaternion.identity);
			emote.SetEmote(_gameConfig.Emotes[emoteIndex]);
		}

		public void SetAsleepCanSee(bool asleepCanSee)
		{
			_asleepCanSee = asleepCanSee;
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_ShowEmote(PlayerRef selectedPlayer, int emoteIndex, RpcInfo info = default)
		{
			if (_playerUsage[info.Source].amount >= _gameConfig.EmoteLimit || !_gameManager.IsPlayerAwake(info.Source))
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
