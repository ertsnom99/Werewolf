using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utilities.GameplayData;
using Werewolf.Data;
using Werewolf.Gameplay;
using Werewolf.Gameplay.Role;
using static Werewolf.Managers.GameHistoryManager;

namespace Werewolf.Managers
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

		private class PlayerGroup
		{
			public UniqueID ID;
			public int Priority;
			public List<PlayerRef> Players;
			public PlayerRef Leader;
		}

		private PlayerRef[] _playersOrder;

		private readonly Dictionary<PlayerRef, Action<PlayerRef[]>> _selectPlayersCallbacks = new();
		private List<PlayerRef> _serverChoices;
		private PlayerRef[] _clientChoices;
		private bool _mustSelectPlayer;
		private int _playerAmountToSelect;
		private readonly List<PlayerRef> _selectedPlayers = new();

		private readonly Dictionary<PlayerRef, Action<PlayerRef>> _promptPlayerCallbacks = new();

		public event Action<PlayerRef, UniqueID> AddedPlayerToPlayerGroup;
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
				if (playerInfo.Key != playerToExclude)
				{
					players.Add(playerInfo.Key);
				}
			}

			return players.ToArray();
		}

		private PlayerRef[] GetPlayersExcluding(PlayerRef[] playersToExclude)
		{
			List<PlayerRef> players = new();

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				if (!playersToExclude.Contains(playerInfo.Key))
				{
					players.Add(playerInfo.Key);
				}
			}

			return players.ToArray();
		}

		public HashSet<PlayerRef> FindSurroundingPlayers(PlayerRef relativeTo)
		{
			HashSet<PlayerRef> SurroundingPlayers = new();

			int playerIndex = Array.IndexOf(_playersOrder, relativeTo);

			FindNextSurroundingPlayer(playerIndex, -1, ref SurroundingPlayers);
			FindNextSurroundingPlayer(playerIndex, 1, ref SurroundingPlayers);

			return SurroundingPlayers;
		}

		private void FindNextSurroundingPlayer(int relativeTo, int iteration, ref HashSet<PlayerRef> SurroundingPlayers)
		{
			int currentIndex = relativeTo;

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
			while (currentIndex != relativeTo);
		}

		public PlayerRef FindNextPlayer(PlayerRef relativeTo, bool searchToLeft, bool mustBeAwake = false, UniqueID[] playerGroupIDsFilter = null)
		{
			int playerIndex = Array.IndexOf(_playersOrder, relativeTo);
			int currentIndex = playerIndex;
			int iteration = searchToLeft ? 1 : -1;

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

				if (PlayerGameInfos[currentPlayer].IsAlive
					&& (!mustBeAwake || IsPlayerAwake(currentPlayer))
					&& (playerGroupIDsFilter == null || IsPlayerInPlayerGroups(currentPlayer, playerGroupIDsFilter)))
				{
					return currentPlayer;
				}
			}
			while (currentIndex != playerIndex);

			return PlayerRef.None;
		}
		#endregion

		#region Highlight Players
		public IEnumerator HighlightPlayerToggle(PlayerRef player, float duration)
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

		public void SetAllPlayersCardHighlightVisible(bool isVisible)
		{
			foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
			{
				playerCard.Value.SetHighlightVisible(isVisible);
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

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_SetAllPlayersCardHighlightVisible(bool isVisible)
		{
			SetAllPlayersCardHighlightVisible(isVisible);
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
			if (_playerCards.TryGetValue(deadPlayer, out Card playerCard) && playerCard)
			{
				playerCard.DisplayDeadIcon();
			}
		}
		#endregion
		#endregion

		#region Player Groups
		public void AddPlayerToPlayerGroup(PlayerRef player, UniqueID playerGroupID)
		{
			if (!_gameplayDataManager.TryGetGameplayData(playerGroupID.HashCode, out PlayerGroupData playerGroupData))
			{
				Debug.LogError($"Could not find the player group {playerGroupID.HashCode}");
				return;
			}

			int priority = playerGroupData.Priority;

			for (int i = 0; i < _playerGroups.Count; i++)
			{
				if (_playerGroups[i].ID.HashCode == playerGroupID.HashCode)
				{
					if (_playerGroups[i].Players.Contains(player))
					{
						return;
					}

					_playerGroups[i].Players.Add(player);
					AddedPlayerToPlayerGroup?.Invoke(player, playerGroupID);
					return;
				}
				else if (_playerGroups[i].Priority < priority)
				{
					_playerGroups.Insert(i, new() { ID = playerGroupID, Priority = priority, Players = new() { player } });
					AddedPlayerToPlayerGroup?.Invoke(player, playerGroupID);
					return;
				}
			}

			_playerGroups.Add(new() { ID = playerGroupID, Priority = priority, Players = new() { player } });
			AddedPlayerToPlayerGroup?.Invoke(player, playerGroupID);
		}

		public void AddPlayersToNewPlayerGroup(PlayerRef[] players, UniqueID playerGroupID)
		{
			if (!_gameplayDataManager.TryGetGameplayData(playerGroupID.HashCode, out PlayerGroupData playerGroupData))
			{
				Debug.LogError($"Could not find the player group {playerGroupID.HashCode}");
				return;
			}

			int priority = playerGroupData.Priority;
			bool AddedPlayers = false;

			for (int i = 0; i < _playerGroups.Count; i++)
			{
				if (_playerGroups[i].Priority <= priority)
				{
					_playerGroups.Insert(i, new() { ID = playerGroupID, Priority = priority, Players = new(players) });
					AddedPlayers = true;
					break;
				}
			}

			if (!AddedPlayers)
			{
				_playerGroups.Add(new() { ID = playerGroupID, Priority = priority, Players = new(players) });
			}

			foreach (PlayerRef player in players)
			{
				AddedPlayerToPlayerGroup?.Invoke(player, playerGroupID);
			}
		}

		public void RemovePlayerFromPlayerGroup(PlayerRef player, UniqueID playerGroupID)
		{
			for (int i = 0; i < _playerGroups.Count; i++)
			{
				if (_playerGroups[i].ID != playerGroupID)
				{
					continue;
				}

				_playerGroups[i].Players.Remove(player);

				if (_playerGroups[i].Players.Count <= 0)
				{
					_playerGroups.RemoveAt(i);
				}
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

		public HashSet<PlayerRef> GetPlayersFromPlayerGroup(UniqueID playerGroupID)
		{
			foreach (PlayerGroup playerGroup in _playerGroups)
			{
				if (playerGroup.ID == playerGroupID)
				{
					return playerGroup.Players.ToHashSet();
				}
			}

			return new();
		}

		public HashSet<PlayerRef> GetPlayersFromPlayerGroups(UniqueID[] playerGroupIDs)
		{
			HashSet<PlayerRef> players = new();

			foreach (PlayerGroup playerGroup in _playerGroups)
			{
				if (playerGroupIDs.Contains(playerGroup.ID))
				{
					players.UnionWith(playerGroup.Players.ToHashSet());
				}
			}

			return players;
		}

		public bool IsPlayerInPlayerGroup(PlayerRef player, UniqueID playerGroupID)
		{
			foreach (PlayerGroup playerGroup in _playerGroups)
			{
				if (playerGroup.ID == playerGroupID && playerGroup.Players.Contains(player))
				{
 					return true;
				}
			}

			return false;
		}

		public bool IsPlayerInPlayerGroups(PlayerRef player, UniqueID[] playerGroupIDs)
		{
			foreach (PlayerGroup playerGroup in _playerGroups)
			{
				if (playerGroupIDs.Contains(playerGroup.ID) && playerGroup.Players.Contains(player))
				{
					return true;
				}
			}

			return false;
		}

		public bool IsAnyPlayersInPlayerGroups(HashSet<PlayerRef> players, UniqueID[] playerGroupIDs)
		{
			foreach (PlayerRef player in players)
			{
				if (IsPlayerInPlayerGroups(player, playerGroupIDs))
				{
					return true;
				}
			}

			return false;
		}

		public void SetPlayerGroupLeader(UniqueID playerGroupID, PlayerRef player)
		{
			foreach (PlayerGroup playerGroup in _playerGroups)
			{
				if (playerGroup.ID == playerGroupID && playerGroup.Players.Contains(player))
				{
					playerGroup.Leader = player;
					return;
				}
			}

			if (!_gameplayDataManager.TryGetGameplayData(playerGroupID.HashCode, out PlayerGroupData playerGroupData))
			{
				Debug.LogError($"Could not find the player group {playerGroupID.HashCode}");
			}

			Debug.LogWarning($"Couldn't set {player} as the leader of {playerGroupData.name}");
		}
		#endregion

		#region Player Awake
		private void SetAllPlayersAwake(bool isAwake)
		{
			PlayerGameInfos.Keys.ToList().ForEach(player => SetPlayerAwake(player, isAwake));
		}

		public void SetPlayerAwake(PlayerRef player, bool isAwake)
		{
			if (PlayerGameInfos[player].IsAlive)
			{
				PlayerGameInfos[player].IsAwake = isAwake;
			}
		}

		public bool IsPlayerAwake(PlayerRef player)
		{
			return PlayerGameInfos[player].IsAwake;

		}
		#endregion

		#region Select Players
		public bool SelectPlayers(PlayerRef selectingPlayer, List<PlayerRef> choices, int titleID, float maximumDuration, bool mustSelect, int playerAmount, ChoicePurpose purpose, Action<PlayerRef[]> callback)
		{
			_serverChoices = choices;
			PreSelectPlayers?.Invoke(selectingPlayer, purpose, _serverChoices);

			if (!_networkDataManager.PlayerInfos[selectingPlayer].IsConnected || _selectPlayersCallbacks.ContainsKey(selectingPlayer) || _serverChoices.Count < playerAmount)
			{
				return false;
			}

			_selectPlayersCallbacks.Add(selectingPlayer, callback);
			RPC_SelectPlayers(selectingPlayer, _serverChoices.ToArray(), titleID, maximumDuration, mustSelect, playerAmount);

			return true;
		}

		private void SelectCard(Card card)
		{
			if (_selectedPlayers.Contains(card.Player))
			{
				if (_selectedPlayers.Count == _playerAmountToSelect)
				{
					SetCardsClickable(true);
				}

				_selectedPlayers.Remove(card.Player);
			}
			else
			{
				_selectedPlayers.Add(card.Player);

				if (_selectedPlayers.Count == _playerAmountToSelect)
				{
					SetCardsClickable(false);
				}
			}

			void SetCardsClickable(bool isClickable)
			{
				foreach (KeyValuePair<PlayerRef, Card> playerCard in _playerCards)
				{
					if (!playerCard.Value || _selectedPlayers.Contains(playerCard.Key) || Array.IndexOf(_clientChoices, playerCard.Key) < 0)
					{
						continue;
					}

					playerCard.Value.SetClickable(isClickable);
				}
			}

			if (_mustSelectPlayer)
			{
				_UIManager.TitleScreen.SetConfirmButtonInteractable(_selectedPlayers.Count == _playerAmountToSelect);
			}
		}

		private void OnConfirmPlayerSelection()
		{
			StopSelectingPlayers();
			RPC_GivePlayerChoices(_selectedPlayers.ToArray());
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

			_UIManager.TitleScreen.ConfirmClicked -= OnConfirmPlayerSelection;
			_UIManager.TitleScreen.SetConfirmButtonInteractable(false);
			_UIManager.TitleScreen.StopCountdown();
		}

		public void StopSelectingPlayers(PlayerRef player)
		{
			_selectPlayersCallbacks.Remove(player);

			if (_networkDataManager.PlayerInfos[player].IsConnected)
			{
				RPC_StopSelectingPlayers(player);
			}
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_SelectPlayers([RpcTarget] PlayerRef player, PlayerRef[] choices, int titleID, float maximumDuration, bool mustSelect, int playerAmount)
		{
			_clientChoices = choices;
			_mustSelectPlayer = mustSelect;
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

			DisplayTitle(titleID, variables: null, showConfirmButton: true, countdownDuration: maximumDuration);
			_UIManager.TitleScreen.ConfirmClicked += OnConfirmPlayerSelection;

			if (mustSelect)
			{
				_UIManager.TitleScreen.SetConfirmButtonInteractable(false);
			}
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_GivePlayerChoices(PlayerRef[] players, RpcInfo info = default)
		{
			if (!_selectPlayersCallbacks.TryGetValue(info.Source, out Action<PlayerRef[]> callback))
			{
				return;
			}

			// Make sure that the client didn't try to cheat by sending invalid choices
			foreach (PlayerRef player in players)
			{
				if (!_serverChoices.Contains(player))
				{
					players = null;
					break;
				}
			}

			callback(players);
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
		public bool PromptPlayer(PlayerRef promptedPlayer, int titleID, float duration, Action<PlayerRef> callback, bool fastFade = true)
		{
			if (!_networkDataManager.PlayerInfos[promptedPlayer].IsConnected || _promptPlayerCallbacks.ContainsKey(promptedPlayer))
			{
				return false;
			}

			_promptPlayerCallbacks.Add(promptedPlayer, callback);
			RPC_PromptPlayer(promptedPlayer, titleID, duration, fastFade);

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
		public void RPC_PromptPlayer([RpcTarget] PlayerRef player, int titleID, float duration, bool fastFade)
		{
			_UIManager.TitleScreen.ConfirmClicked += OnPromptAccepted;
			DisplayTitle(titleID, showConfirmButton: true, countdownDuration: duration, fastFade: fastFade);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_AcceptPrompt(RpcInfo info = default)
		{
			if (!_promptPlayerCallbacks.TryGetValue(info.Source, out var callback))
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

			if (CurrentGameplayLoopStep == GameplayLoopStep.NightCall && PlayersWaitingFor.Contains(player) && PlayerGameInfos[player].Behaviors.Count > 0)
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

			_gameHistoryManager.AddEntry(Config.PlayerDisconnectedGameHistoryEntry.ID,
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