using Assets.Scripts.Data.Tags;
using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using static Werewolf.GameHistoryManager;

namespace Werewolf
{
	public class PlayerGameInfo
	{
		public RoleData Role;
		public List<RoleBehavior> Behaviors;
		public bool IsAwake;
		public bool IsAlive;
	}

	public partial class GameManager
	{
		public Dictionary<PlayerRef, PlayerGameInfo> PlayerGameInfos { get; private set; }

		private readonly List<PlayerGroup> _playerGroups = new();

		private struct PlayerGroup
		{
			public GameplayTag GameplayTag;
			public int Priority;
			public List<PlayerRef> Players;
		}

		private PlayerRef[] _playersOrder;

		private readonly Dictionary<PlayerRef, Action<PlayerRef[]>> _selectPlayersCallbacks = new();
		private List<PlayerRef> _choices;
		private readonly List<PlayerRef> _selectedPlayers = new();
		private int _playerAmountToSelect;

		private readonly Dictionary<PlayerRef, Action<PlayerRef>> _promptPlayerCallbacks = new();

		public event Action<PlayerRef, ChoicePurpose, List<PlayerRef>> PreSelectPlayers;
		public event Action<PlayerRef> PostPlayerDisconnected;

		private void CreatePlayersOrder()
		{
			_playersOrder = new PlayerRef[PlayerGameInfos.Count];
			List<PlayerRef> tempPlayers = PlayerGameInfos.Keys.ToList();

			while (tempPlayers.Count > 0)
			{
				int randomIndex = UnityEngine.Random.Range(0, tempPlayers.Count - 1);
				_playersOrder[^tempPlayers.Count] = tempPlayers[randomIndex];
				tempPlayers.RemoveAt(randomIndex);
			}
		}

		#region Get Players
		public List<PlayerRef> GetAlivePlayers()
		{
			List<PlayerRef> alivePlayers = new();

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (playerInfo.Value.IsAlive)
				{
					alivePlayers.Add(playerInfo.Key);
				}
			}

			return alivePlayers;
		}

		public List<PlayerRef> GetDeadPlayers()
		{
			List<PlayerRef> deadPlayers = new();

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (!playerInfo.Value.IsAlive)
				{
					deadPlayers.Add(playerInfo.Key);
				}
			}

			return deadPlayers;
		}

		private PlayerRef[] GetPlayersExcluding(PlayerRef playerToExclude)
		{
			List<PlayerRef> players = new();

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (playerInfo.Key == playerToExclude)
				{
					continue;
				}

				players.Add(playerInfo.Key);
			}

			return players.ToArray();
		}

		private PlayerRef[] GetPlayersExcluding(PlayerRef[] playersToExclude)
		{
			List<PlayerRef> players = new();

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (playersToExclude.Contains(playerInfo.Key))
				{
					continue;
				}

				players.Add(playerInfo.Key);
			}

			return players.ToArray();
		}

		public List<PlayerRef> FindSurroundingPlayers(PlayerRef player)
		{
			List<PlayerRef> SurroundingPlayers = new();

			int playerIndex = Array.IndexOf(_playersOrder, player);

			FindNextSurroundingPlayer(playerIndex, -1, ref SurroundingPlayers);
			FindNextSurroundingPlayer(playerIndex, 1, ref SurroundingPlayers);

			return SurroundingPlayers;
		}

		private void FindNextSurroundingPlayer(int playerIndex, int iteration, ref List<PlayerRef> SurroundingPlayers)
		{
			int currentIndex = playerIndex;

			do
			{
				currentIndex += iteration;

				if (currentIndex < 0)
				{
					currentIndex = _playersOrder.Length - 1;
				}
				else if (currentIndex >= _playersOrder.Length)
				{
					currentIndex = 0;
				}

				PlayerRef currentPlayer = _playersOrder[currentIndex];

				if (PlayerGameInfos[currentPlayer].IsAlive && !SurroundingPlayers.Contains(currentPlayer))
				{
					SurroundingPlayers.Add(currentPlayer);
					break;
				}
			}
			while (currentIndex != playerIndex);
		}
		#endregion

		#region Highlight Players
		private IEnumerator HighlightPlayerToggle(PlayerRef player, float duration)
		{
			RPC_SetPlayerCardHighlightVisible(player, true);
#if UNITY_SERVER && UNITY_EDITOR
			SetPlayerCardHighlightVisible(player, true);
#endif
			yield return new WaitForSeconds(duration);

			RPC_SetPlayerCardHighlightVisible(player, false);
#if UNITY_SERVER && UNITY_EDITOR
			SetPlayerCardHighlightVisible(player, false);
#endif
		}

		public void SetPlayerCardHighlightVisible(PlayerRef player, bool isVisible)
		{
			_playerCards[player].SetHighlightVisible(isVisible);
		}

		public void SetPlayersCardHighlightVisible(PlayerRef[] players, bool isVisible)
		{
			foreach (PlayerRef player in players)
			{
				_playerCards[player].SetHighlightVisible(isVisible);
			}
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_SetPlayerCardHighlightVisible([RpcTarget] PlayerRef player, PlayerRef highlightedPlayer, bool isVisible)
		{
			SetPlayerCardHighlightVisible(highlightedPlayer, isVisible);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_SetPlayerCardHighlightVisible(PlayerRef highlightedPlayer, bool isVisible)
		{
			SetPlayerCardHighlightVisible(highlightedPlayer, isVisible);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_SetPlayersCardHighlightVisible([RpcTarget] PlayerRef player, PlayerRef[] highlightedPlayers, bool isVisible)
		{
			SetPlayersCardHighlightVisible(highlightedPlayers, isVisible);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_SetPlayersCardHighlightVisible(PlayerRef[] highlightedPlayers, bool isVisible)
		{
			SetPlayersCardHighlightVisible(highlightedPlayers, isVisible);
		}
		#endregion
		#endregion

		#region Player Icons
		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_SetPlayersCardWerewolfIconVisible([RpcTarget] PlayerRef player, PlayerRef[] werewolfPlayers, bool isVisible)
		{
			foreach (PlayerRef werewolfPlayer in werewolfPlayers)
			{
				_playerCards[werewolfPlayer].DisplayWerewolfIcon(isVisible);
			}
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayPlayerDeadIcon(PlayerRef deadPlayer)
		{
			if (!_playerCards.ContainsKey(deadPlayer) || !_playerCards[deadPlayer])
			{
				return;
			}

			_playerCards[deadPlayer].DisplayDeadIcon();
		}
		#endregion
		#endregion

		#region Player Groups
		public void AddPlayerToPlayerGroup(PlayerRef player, GameplayTag playerGroup)
		{
			int priority = _gameplayDatabaseManager.GetGameplayData<PlayerGroupData>(playerGroup.CompactTagId).Priority;

			for (int i = 0; i < _playerGroups.Count; i++)
			{
				if (_playerGroups[i].GameplayTag == playerGroup)
				{
					if (_playerGroups[i].Players.Contains(player))
					{
						Debug.LogError("Tried to add duplicated player to a player group");
						return;
					}

					_playerGroups[i].Players.Add(player);
					return;
				}
				else if (_playerGroups[i].Priority < priority)
				{
					_playerGroups.Insert(i, new() { GameplayTag = playerGroup, Priority = priority, Players = new() { player } });
					return;
				}
			}

			_playerGroups.Add(new() { GameplayTag = playerGroup, Priority = priority, Players = new() { player } });
		}

		public void RemovePlayerFromGroup(PlayerRef player, GameplayTag playerGroup)
		{
			for (int i = 0; i < _playerGroups.Count; i++)
			{
				if (_playerGroups[i].GameplayTag != playerGroup)
				{
					continue;
				}

				_playerGroups[i].Players.Remove(player);

				if (_playerGroups[i].Players.Count <= 0)
				{
					_playerGroups.RemoveAt(i);
				}

				break;
			}
		}

		public void RemovePlayerFromAllPlayerGroups(PlayerRef player)
		{
			for (int i = _playerGroups.Count - 1; i >= 0; i--)
			{
				_playerGroups[i].Players.Remove(player);

				if (_playerGroups[i].Players.Count <= 0)
				{
					_playerGroups.RemoveAt(i);
				}
			}
		}

		public List<PlayerRef> GetPlayersFromGroup(GameplayTag[] inPlayerGroup)
		{
			List<PlayerRef> players = new();

			foreach (PlayerGroup playerGroup in _playerGroups)
			{
				if (inPlayerGroup.Contains(playerGroup.GameplayTag))
				{
					players.AddRange(playerGroup.Players.Except(players));
				}
			}

			return players;
		}

		public bool IsPlayerInPlayerGroups(PlayerRef player, GameplayTag[] inPlayerGroups)
		{
			bool inPlayerGroup = false;

			foreach (PlayerGroup playerGroup in _playerGroups)
			{
				if (inPlayerGroups.Contains(playerGroup.GameplayTag) && playerGroup.Players.Contains(player))
				{
					inPlayerGroup = true;
					break;
				}
			}

			return inPlayerGroup;
		}
		#endregion

		#region Player Awake
		private void SetAllPlayersAwake(bool isAwake)
		{
			PlayerGameInfos.Keys.ToList().ForEach(player => SetPlayerAwake(player, isAwake));
		}

		public void SetPlayerAwake(PlayerRef player, bool isAwake)
		{
			if (!PlayerGameInfos[player].IsAlive)
			{
				return;
			}

			PlayerGameInfos[player].IsAwake = isAwake;
		}
		#endregion

		#region Select Players
		public bool SelectPlayers(PlayerRef selectingPlayer, List<PlayerRef> choices, int imageID, float maximumDuration, bool mustSelect, int playerAmount, ChoicePurpose purpose, Action<PlayerRef[]> callback)
		{
			_choices = choices;
			PreSelectPlayers?.Invoke(selectingPlayer, purpose, _choices);

			if (!_networkDataManager.PlayerInfos[selectingPlayer].IsConnected || _selectPlayersCallbacks.ContainsKey(selectingPlayer) || _choices.Count < playerAmount)
			{
				return false;
			}

			_selectPlayersCallbacks.Add(selectingPlayer, callback);
			RPC_SelectPlayers(selectingPlayer, _choices.ToArray(), imageID, maximumDuration, mustSelect, playerAmount);

			return true;
		}

		private void SelectNoPlayer()
		{
			StopSelectingPlayers();
			RPC_GivePlayerChoices(new PlayerRef[0]);
		}

		private void SelectCard(Card card)
		{
			if (_selectedPlayers.Contains(card.Player))
			{
				_selectedPlayers.Remove(card.Player);

				return;
			}

			_selectedPlayers.Add(card.Player);

			if (_selectedPlayers.Count < _playerAmountToSelect)
			{
				return;
			}

			StopSelectingPlayers();
			RPC_GivePlayerChoices(_selectedPlayers.ToArray());
		}

		public void StopSelectingPlayers(PlayerRef player)
		{
			_selectPlayersCallbacks.Remove(player);

			if (!_networkDataManager.PlayerInfos[player].IsConnected)
			{
				return;
			}

			RPC_StopSelectingPlayers(player);
		}

		private void StopSelectingPlayers()
		{
			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				if (!playerCard.Value)
				{
					continue;
				}

				playerCard.Value.ResetSelectionMode();
				playerCard.Value.LeftClicked -= SelectCard;
			}

			foreach (PlayerRef selectedPlayer in _selectedPlayers)
			{
				SetPlayerCardHighlightVisible(selectedPlayer, false);
			}

			_UIManager.TitleScreen.ConfirmClicked -= SelectNoPlayer;
			_UIManager.TitleScreen.SetConfirmButtonInteractable(false);
			_UIManager.TitleScreen.StopCountdown();
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_SelectPlayers([RpcTarget] PlayerRef player, PlayerRef[] choices, int imageID, float maximumDuration, bool mustSelect, int playerAmount)
		{
			_playerAmountToSelect = playerAmount;
			_selectedPlayers.Clear();

			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				if (!playerCard.Value)
				{
					continue;
				}

				if (Array.IndexOf(choices, playerCard.Key) < 0)
				{
					playerCard.Value.SetSelectionMode(true, false);
					continue;
				}

				playerCard.Value.SetSelectionMode(true, true);
				playerCard.Value.LeftClicked += SelectCard;
			}

			DisplayTitle(imageID, variables: null, showConfirmButton: !mustSelect, countdownDuration: maximumDuration);

			if (mustSelect)
			{
				return;
			}

			_UIManager.TitleScreen.ConfirmClicked += SelectNoPlayer;
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_GivePlayerChoices(PlayerRef[] players, RpcInfo info = default)
		{
			if (!_selectPlayersCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			// Make sure that the client didn't try to cheat by sending invalid choices
			foreach (PlayerRef player in players)
			{
				if (!_choices.Contains(player))
				{
					players = null;
					break;
				}
			}

			_selectPlayersCallbacks[info.Source](players);
			_selectPlayersCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_StopSelectingPlayers([RpcTarget] PlayerRef player)
		{
			StopSelectingPlayers();
		}
		#endregion
		#endregion

		#region Prompt Player
		public bool PromptPlayer(PlayerRef promptedPlayer, int imageID, float duration, Action<PlayerRef> callback, bool fastFade = true)
		{
			if (!_networkDataManager.PlayerInfos[promptedPlayer].IsConnected || _promptPlayerCallbacks.ContainsKey(promptedPlayer))
			{
				return false;
			}

			_promptPlayerCallbacks.Add(promptedPlayer, callback);
			RPC_PromptPlayer(promptedPlayer, imageID, duration, fastFade);

			return true;
		}

		private void OnPromptAccepted()
		{
			_UIManager.TitleScreen.ConfirmClicked -= OnPromptAccepted;
			RPC_AcceptPrompt();
		}

		public void StopPromptingPlayer(PlayerRef player, bool fastFade = true)
		{
			if (!_networkDataManager.PlayerInfos[player].IsConnected)
			{
				return;
			}

			_promptPlayerCallbacks.Remove(player);
			RPC_StopPromptingPlayer(player, fastFade);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_PromptPlayer([RpcTarget] PlayerRef player, int imageID, float duration, bool fastFade)
		{
			_UIManager.TitleScreen.ConfirmClicked += OnPromptAccepted;
			DisplayTitle(imageID, showConfirmButton: true, countdownDuration: duration, fastFade: fastFade);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_AcceptPrompt(RpcInfo info = default)
		{
			if (!_promptPlayerCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_promptPlayerCallbacks[info.Source](info.Source);
			_promptPlayerCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_StopPromptingPlayer([RpcTarget] PlayerRef player, bool fastFade)
		{
			_UIManager.TitleScreen.ConfirmClicked -= OnPromptAccepted;
			_UIManager.FadeOut(_UIManager.TitleScreen, fastFade ? Config.UITransitionFastDuration : Config.UITransitionNormalDuration);
		}
		#endregion
		#endregion

		#region Player Disconnection
		private void OnPlayerDisconnected(PlayerRef player)
		{
			if (PlayerGameInfos[player].IsAlive)
			{
				AddMarkForDeath(player, Config.PlayerLeftMarkForDeath);
			}

			if (_currentGameplayLoopStep == GameplayLoopStep.NightCall && PlayersWaitingFor.Contains(player) && PlayerGameInfos[player].Behaviors.Count > 0)
			{
				for (int i = 0; i < PlayerGameInfos[player].Behaviors.Count; i++)
				{
					PlayerGameInfos[player].Behaviors[i].OnRoleCallDisconnected();
				}
			}

			StopWaintingForPlayer(player);

			_chooseReservedRoleCallbacks.Remove(player);
			_selectPlayersCallbacks.Remove(player);
			_makeChoiceCallbacks.Remove(player);
			_revealPlayerRoleCallbacks.Remove(player);
			_moveCardToCameraCallbacks.Remove(player);
			_flipCardCallbacks.Remove(player);
			_putCardBackDownCallbacks.Remove(player);

			PostPlayerDisconnected?.Invoke(player);

			_gameHistoryManager.AddEntry(Config.PlayerDisconnectedGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "Player",
												Data = _networkDataManager.PlayerInfos[player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			RPC_DisplayPlayerDisconnected(player);
#if UNITY_SERVER && UNITY_EDITOR
			_UIManager.DisconnectedScreen.DisplayDisconnectedPlayer(player);
#endif
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DisplayPlayerDisconnected(PlayerRef player)
		{
			_UIManager.DisconnectedScreen.DisplayDisconnectedPlayer(player);
		}
		#endregion
		#endregion
	}
}