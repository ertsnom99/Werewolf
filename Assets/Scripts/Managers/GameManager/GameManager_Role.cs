using Assets.Scripts.Data.Tags;
using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using Werewolf.UI;

namespace Werewolf
{
	public partial class GameManager
	{
		private Dictionary<RoleBehavior, RoleData> _unassignedRoleBehaviors = new();

		[Networked, Capacity(5)]
		public NetworkArray<RolesContainer> ReservedRoles { get; }
		private Dictionary<RoleBehavior, IndexedReservedRoles> _reservedRolesByBehavior = new();

		public struct RolesContainer : INetworkStruct
		{
			public int RoleCount;
			[Networked, Capacity(5)]
			public NetworkArray<int> Roles { get; }
		}

		public struct IndexedReservedRoles
		{
			public RoleData[] Roles;
			public RoleBehavior[] Behaviors;
			public int networkIndex;
		}
#if UNITY_SERVER && UNITY_EDITOR
		private Dictionary<RoleBehavior, Card[]> _reservedCardsByBehavior = new();
#endif
		private Dictionary<PlayerRef, Action<int>> _chooseReservedRoleCallbacks = new();
		private Card[][] _reservedRolesCards;

		private Dictionary<PlayerRef, Action<PlayerRef>> _revealPlayerRoleCallbacks = new();
		private Dictionary<PlayerRef, Action> _moveCardToCameraCallbacks = new();
		private Dictionary<PlayerRef, Action> _flipCardCallbacks = new();
		private Dictionary<PlayerRef, Action> _putCardBackDownCallbacks = new();

		#region Role Change
		public void ChangeRole(PlayerRef player, RoleData roleData, RoleBehavior roleBehavior)
		{
			RemovePrimaryBehavior(player);

			if (!roleBehavior)
			{
				foreach (GameplayTag playerGroup in roleData.PlayerGroups)
				{
					AddPlayerToPlayerGroup(player, playerGroup);
				}
			}
			else
			{
				AddBehavior(player, roleBehavior);
				roleBehavior.SetIsPrimaryBehavior(true);
			}

			PlayerGameInfos[player] = new() { Role = roleData, Behaviors = PlayerGameInfos[player].Behaviors, IsAlive = PlayerGameInfos[player].IsAlive };

			if (_networkDataManager.PlayerInfos[player].IsConnected)
			{
				RPC_ChangePlayerCardRole(player, roleData.GameplayTag.CompactTagId);
			}
#if UNITY_SERVER && UNITY_EDITOR
			ChangePlayerCardRole(player, roleData);
#endif
		}

		public void TransferRole(PlayerRef from, PlayerRef to, bool destroyOldBehavior = true, bool reInitNewBehavior = false)
		{
			RemovePrimaryBehavior(to, destroyOldBehavior);

			if (PlayerGameInfos[from].Behaviors.Count <= 0)
			{
				foreach (GameplayTag villageGroup in PlayerGameInfos[from].Role.PlayerGroups)
				{
					AddPlayerToPlayerGroup(to, villageGroup);
				}
			}
			else
			{
				foreach (RoleBehavior behavior in PlayerGameInfos[from].Behaviors)
				{
					if (behavior.IsPrimaryBehavior)
					{
						RemoveBehavior(from, behavior, destroyBehavior: false);
						AddBehavior(to, behavior, reInitBehavior: reInitNewBehavior);
						break;
					}
				}
			}

			PlayerGameInfos[to] = new() { Role = PlayerGameInfos[from].Role, Behaviors = PlayerGameInfos[to].Behaviors, IsAlive = PlayerGameInfos[to].IsAlive };
			PlayerGameInfos[from] = new() { Role = null, Behaviors = PlayerGameInfos[from].Behaviors, IsAlive = PlayerGameInfos[from].IsAlive };

			if (_networkDataManager.PlayerInfos[to].IsConnected)
			{
				RPC_ChangePlayerCardRole(to, PlayerGameInfos[to].Role.GameplayTag.CompactTagId);
			}
#if UNITY_SERVER && UNITY_EDITOR
			ChangePlayerCardRole(to, PlayerGameInfos[to].Role);
#endif
		}

		public void ChangePlayerCardRole(PlayerRef player, RoleData roleData)
		{
			_playerCards[player].SetRole(roleData);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_ChangePlayerCardRole([RpcTarget] PlayerRef player, int roleGameplayTagID)
		{
			ChangePlayerCardRole(player, _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTagID));
		}
		#endregion
		#endregion

		#region Behavior Change
		public void AddBehavior(PlayerRef player, RoleBehavior behavior, bool addPlayerToPlayerGroup = true, bool reInitBehavior = false)
		{
			int[] nightPrioritiesIndexes = behavior.GetNightPrioritiesIndexes();

			// Remove any contradicting behaviors
			List<RoleBehavior> behaviorsToRemove = FindNightCallBehaviors(player, nightPrioritiesIndexes);

			foreach (RoleBehavior behaviorToRemove in behaviorsToRemove)
			{
				RemoveBehavior(player, behaviorToRemove);
			}

			foreach (int priority in nightPrioritiesIndexes)
			{
				AddPlayerToNightCall(priority, player);
			}

			if (addPlayerToPlayerGroup)
			{
				foreach (GameplayTag playerGroup in behavior.GetCurrentPlayerGroups())
				{
					AddPlayerToPlayerGroup(player, playerGroup);
				}
			}

			PlayerGameInfos[player].Behaviors.Add(behavior);
			behavior.SetPlayer(player);

			if (reInitBehavior)
			{
				behavior.ReInitialize();
			}

#if UNITY_SERVER && UNITY_EDITOR
			behavior.transform.position = _playerCards[player].transform.position;
#endif
		}

		private void RemovePrimaryBehavior(PlayerRef player, bool destroyOldBehavior = true)
		{
			if (PlayerGameInfos[player].Behaviors.Count <= 0)
			{
				foreach (GameplayTag playerGroup in PlayerGameInfos[player].Role.PlayerGroups)
				{
					RemovePlayerFromGroup(player, playerGroup);
				}
			}
			else
			{
				foreach (RoleBehavior behavior in PlayerGameInfos[player].Behaviors)
				{
					if (behavior.IsPrimaryBehavior)
					{
						RemoveBehavior(player, behavior, destroyBehavior: destroyOldBehavior);
						break;
					}
				}
			}
		}

		public void RemoveBehavior(PlayerRef player, RoleBehavior behavior, bool removePlayerFromGroup = true, bool destroyBehavior = true)
		{
			int[] nightPrioritiesIndexes = behavior.GetNightPrioritiesIndexes();

			foreach (int priority in nightPrioritiesIndexes)
			{
				RemovePlayerFromNightCall(priority, player);
			}

			for (int i = PlayerGameInfos[player].Behaviors.Count - 1; i >= 0; i--)
			{
				if (PlayerGameInfos[player].Behaviors[i] != behavior)
				{
					continue;
				}

				PlayerGameInfos[player].Behaviors.RemoveAt(i);
				break;
			}

			if (removePlayerFromGroup)
			{
				foreach (GameplayTag playerGroup in behavior.GetCurrentPlayerGroups())
				{
					RemovePlayerFromGroup(player, playerGroup);
				}
			}

			behavior.SetPlayer(PlayerRef.None);

			if (!destroyBehavior)
			{
				return;
			}

			Destroy(behavior.gameObject);
		}

		// Returns all the RoleBehavior that are called during a night call and that have at least one of the prioritiesIndex
		private List<RoleBehavior> FindNightCallBehaviors(PlayerRef player, int[] prioritiesIndex)
		{
			List<RoleBehavior> behaviorsToRemove = new();

			foreach (RoleBehavior behavior in PlayerGameInfos[player].Behaviors)
			{
				int[] nightPrioritiesIndexes = behavior.GetNightPrioritiesIndexes();

				foreach (int behaviorNightPriority in nightPrioritiesIndexes)
				{
					if (prioritiesIndex.Contains(behaviorNightPriority) && !behaviorsToRemove.Contains(behavior))
					{
						behaviorsToRemove.Add(behavior);
						break;
					}
				}
			}

			return behaviorsToRemove;
		}
		#endregion

		#region Role Reservation
		public void ReserveRoles(RoleBehavior roleBehavior, RoleData[] roles, bool areFaceUp, bool arePrimaryBehavior)
		{
			RolesContainer rolesContainer = new();
			RoleBehavior[] behaviors = new RoleBehavior[roles.Length];

			rolesContainer.RoleCount = roles.Length;

			for (int i = 0; i < roles.Length; i++)
			{
				rolesContainer.Roles.Set(i, areFaceUp ? roles[i].GameplayTag.CompactTagId : -1);

				foreach (KeyValuePair<RoleBehavior, RoleData> unassignedRoleBehavior in _unassignedRoleBehaviors)
				{
					if (unassignedRoleBehavior.Value == roles[i])
					{
						behaviors[i] = unassignedRoleBehavior.Key;
						behaviors[i].SetIsPrimaryBehavior(arePrimaryBehavior);
						_unassignedRoleBehaviors.Remove(unassignedRoleBehavior.Key);
						break;
					}
				}
			}

			ReservedRoles.Set(_reservedRolesByBehavior.Count, rolesContainer);
			_reservedRolesByBehavior.Add(roleBehavior, new() { Roles = roles, Behaviors = behaviors, networkIndex = _reservedRolesByBehavior.Count });
		}

		public IndexedReservedRoles GetReservedRoles(RoleBehavior roleBehavior)
		{
			IndexedReservedRoles reservedRoles = new();

			if (_reservedRolesByBehavior.ContainsKey(roleBehavior))
			{
				reservedRoles = _reservedRolesByBehavior[roleBehavior];
			}

			return reservedRoles;
		}

		public void RemoveReservedRoles(RoleBehavior ReservedRoleOwner, int[] specificIndexes)
		{
			if (!_reservedRolesByBehavior.ContainsKey(ReservedRoleOwner))
			{
				return;
			}

			int networkIndex = _reservedRolesByBehavior[ReservedRoleOwner].networkIndex;
			bool mustRemoveEntry = true;

			if (specificIndexes.Length > 0)
			{
				foreach (int specificIndex in specificIndexes)
				{
					RoleBehavior behavior = _reservedRolesByBehavior[ReservedRoleOwner].Behaviors[specificIndex];

					if (behavior && behavior.Player == null)
					{
						Destroy(behavior.gameObject);
					}

					_reservedRolesByBehavior[ReservedRoleOwner].Roles[specificIndex] = null;
					_reservedRolesByBehavior[ReservedRoleOwner].Behaviors[specificIndex] = null;
#if UNITY_SERVER && UNITY_EDITOR
					if (_reservedCardsByBehavior[ReservedRoleOwner][specificIndex])
					{
						Destroy(_reservedCardsByBehavior[ReservedRoleOwner][specificIndex].gameObject);
					}

					_reservedCardsByBehavior[ReservedRoleOwner][specificIndex] = null;
#endif
					// Update networked variable
					// Networked data and server data should ALWAYS be aligned, therefore no need to loop to find the corresponding role
					RolesContainer rolesContainer = new();
					rolesContainer.RoleCount = ReservedRoles[networkIndex].RoleCount;

					for (int i = 0; i < rolesContainer.Roles.Length; i++)
					{
						if (i == specificIndex)
						{
							continue;
						}

						rolesContainer.Roles.Set(i, ReservedRoles[networkIndex].Roles.Get(i));
					}

					ReservedRoles.Set(networkIndex, rolesContainer);

					// Check if the entry is now empty and can be removed
					for (int i = 0; i < _reservedRolesByBehavior[ReservedRoleOwner].Roles.Length; i++)
					{
						if (_reservedRolesByBehavior[ReservedRoleOwner].Roles[i])
						{
							mustRemoveEntry = false;
						}
					}
				}
			}
			else
			{
				// Update server variables
				for (int i = 0; i < _reservedRolesByBehavior[ReservedRoleOwner].Roles.Length; i++)
				{
					RoleBehavior behavior = _reservedRolesByBehavior[ReservedRoleOwner].Behaviors[i];

					if (behavior && behavior.Player == null)
					{
						Destroy(behavior.gameObject);
					}
#if UNITY_SERVER && UNITY_EDITOR
					if (_reservedCardsByBehavior[ReservedRoleOwner][i])
					{
						Destroy(_reservedCardsByBehavior[ReservedRoleOwner][i].gameObject);
					}
#endif
				}

				// Update networked variable
				RolesContainer rolesContainer = new();
				ReservedRoles.Set(networkIndex, rolesContainer);
			}

			// Update server variable entry
			if (mustRemoveEntry)
			{
				_reservedRolesByBehavior.Remove(ReservedRoleOwner);
#if UNITY_SERVER && UNITY_EDITOR
				_reservedCardsByBehavior.Remove(ReservedRoleOwner);
#endif
			}

			// Tell clients to update visual on there side
			RPC_UpdateDisplayedReservedRole(networkIndex);
		}

		// Returns if there is any reserved roles the player can choose from (will be false if the behavior is already waiting for a callback from this method)
		public bool AskClientToChooseReservedRole(RoleBehavior ReservedRoleOwner, float maximumDuration, bool mustChoose, Action<int> callback)
		{
			if (!_networkDataManager.PlayerInfos[ReservedRoleOwner.Player].IsConnected || !_reservedRolesByBehavior.ContainsKey(ReservedRoleOwner) || _chooseReservedRoleCallbacks.ContainsKey(ReservedRoleOwner.Player))
			{
				return false;
			}

			RoleData[] roleDatas = _reservedRolesByBehavior[ReservedRoleOwner].Roles;
			RolesContainer rolesContainer = new() { RoleCount = roleDatas.Length };

			for (int i = 0; i < roleDatas.Length; i++)
			{
				if (!roleDatas[i])
				{
					continue;
				}

				rolesContainer.Roles.Set(i, roleDatas[i].GameplayTag.CompactTagId);
			}

			_chooseReservedRoleCallbacks.Add(ReservedRoleOwner.Player, callback);
			RPC_ClientChooseReservedRole(ReservedRoleOwner.Player, maximumDuration, rolesContainer, mustChoose);

			return true;
		}

		private void GiveReservedRoleChoice(int choice)
		{
			_UIManager.ChoiceScreen.ConfirmedChoice -= GiveReservedRoleChoice;
			RPC_GiveReservedRoleChoice(choice);
		}

		public void StopChoosingReservedRole(PlayerRef reservedRoleOwner)
		{
			_chooseReservedRoleCallbacks.Remove(reservedRoleOwner);

			if (!_networkDataManager.PlayerInfos[reservedRoleOwner].IsConnected)
			{
				return;
			}

			RPC_ClientStopChoosingReservedRole(reservedRoleOwner);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_ClientChooseReservedRole([RpcTarget] PlayerRef player, float maximumDuration, RolesContainer rolesContainer, bool mustChooseOne)
		{
			List<Choice.ChoiceData> choices = new();

			foreach (int roleGameplayTag in rolesContainer.Roles)
			{
				if (roleGameplayTag <= 0)
				{
					continue;
				}

				RoleData roleData = _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTag);
				choices.Add(new() { Image = roleData.Image, Name = roleData.name });
			}

			_UIManager.ChoiceScreen.ConfirmedChoice += GiveReservedRoleChoice;

			_UIManager.ChoiceScreen.Initialize(maximumDuration, mustChooseOne ? Config.ChooseRoleObligatoryText : Config.ChooseRoleText, Config.ChoosedRoleText, Config.DidNotChoosedRoleText, choices.ToArray(), mustChooseOne);
			_UIManager.FadeIn(_UIManager.ChoiceScreen, Config.UITransitionNormalDuration);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_GiveReservedRoleChoice(int roleGameplayTagID, RpcInfo info = default)
		{
			if (!_chooseReservedRoleCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_chooseReservedRoleCallbacks[info.Source](roleGameplayTagID);
			_chooseReservedRoleCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_UpdateDisplayedReservedRole(int networkIndex)
		{
			RolesContainer rolesContainer = ReservedRoles[networkIndex];
			for (int i = 0; i < rolesContainer.Roles.Count(); i++)
			{
				if (rolesContainer.Roles[i] != 0 || _reservedRolesCards[networkIndex].Length <= i || !_reservedRolesCards[networkIndex][i])
				{
					continue;
				}

				Destroy(_reservedRolesCards[networkIndex][i].gameObject);
			}
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_ClientStopChoosingReservedRole([RpcTarget] PlayerRef player)
		{
			_UIManager.ChoiceScreen.StopCountdown();
			_UIManager.ChoiceScreen.DisableConfirmButton();
			_UIManager.ChoiceScreen.ConfirmedChoice -= GiveReservedRoleChoice;
		}
		#endregion
		#endregion

		#region Role Reveal
		public bool RevealPlayerRole(PlayerRef playerRevealed, PlayerRef revealTo, bool waitBeforeReveal, bool returnFaceDown, Action<PlayerRef> callback)
		{
			if (!_networkDataManager.PlayerInfos[revealTo].IsConnected || _revealPlayerRoleCallbacks.ContainsKey(revealTo))
			{
				return false;
			}

			_revealPlayerRoleCallbacks.Add(revealTo, callback);
			RPC_RevealPlayerRole(revealTo, playerRevealed, PlayerGameInfos[playerRevealed].Role.GameplayTag.CompactTagId, waitBeforeReveal, returnFaceDown);

			return true;
		}

		private IEnumerator RevealPlayerRole(Card card, bool waitBeforeReveal, bool returnFaceDown)
		{
			yield return MoveCardToCamera(card.transform, !waitBeforeReveal, Config.MoveToCameraDuration);

			if (waitBeforeReveal)
			{
				yield return new WaitForSeconds(Config.RoleRevealWaitDuration);
				yield return FlipCard(card.transform, Config.RoleRevealFlipDuration);
			}

			yield return new WaitForSeconds(Config.RoleRevealHoldDuration);
			yield return PutCardBackDown(card, returnFaceDown, Config.MoveToCameraDuration);

			RPC_RevealPlayerRoleFinished();
		}

		public void MoveCardToCamera(PlayerRef cardPlayer, bool showRevealed, Action MovementCompleted = null)
		{
			StartCoroutine(MoveCardToCamera(_playerCards[cardPlayer].transform, showRevealed, Config.MoveToCameraDuration, MovementCompleted));
		}

		private IEnumerator MoveCardToCamera(Transform card, bool showRevealed, float duration, Action MovementCompleted = null)
		{
			Camera mainCamera = Camera.main;

			Vector3 startingPosition = card.position;
			Vector3 targetPosition = mainCamera.transform.position + mainCamera.transform.forward * Config.RoleRevealDistanceToCamera;

			Quaternion startingRotation = card.transform.rotation;
			Quaternion targetRotation;

			float elapsedTime = .0f;

			if (showRevealed)
			{
				targetRotation = Quaternion.LookRotation(mainCamera.transform.up, mainCamera.transform.forward);
			}
			else
			{
				targetRotation = Quaternion.LookRotation(mainCamera.transform.up, -mainCamera.transform.forward);
			}

			while (elapsedTime < duration)
			{
				yield return 0;

				elapsedTime += Time.deltaTime;
				float progress = elapsedTime / duration;

				card.transform.position = Vector3.Lerp(startingPosition, targetPosition, progress);
				card.transform.rotation = Quaternion.Lerp(startingRotation, targetRotation, progress);
			}

			MovementCompleted?.Invoke();
		}

		private bool MoveCardToCamera(PlayerRef movedFor, PlayerRef cardPlayer, bool showRevealed, int gameplayDataID, Action movementCompleted)
		{
			if (!_networkDataManager.PlayerInfos[movedFor].IsConnected || _moveCardToCameraCallbacks.ContainsKey(movedFor))
			{
				return false;
			}

			_moveCardToCameraCallbacks.Add(movedFor, movementCompleted);
			RPC_MoveCardToCamera(movedFor, cardPlayer, showRevealed, gameplayDataID);

			return true;
		}

		public void FlipCard(PlayerRef cardPlayer, int gameplayDataID = -1, Action FlipCompleted = null)
		{
			if (gameplayDataID == -1)
			{
				_playerCards[cardPlayer].SetRole(null);
			}
			else
			{
				_playerCards[cardPlayer].SetRole(_gameplayDatabaseManager.GetGameplayData<RoleData>(gameplayDataID));
			}

			StartCoroutine(FlipCard(_playerCards[cardPlayer].transform, Config.RoleRevealFlipDuration, FlipCompleted));
		}

		private IEnumerator FlipCard(Transform card, float duration, Action FlipCompleted = null)
		{
			float elapsedTime = .0f;

			Quaternion startingRotation = card.rotation;
			Quaternion targetRotation = Quaternion.LookRotation(card.forward, -card.up);

			while (elapsedTime < duration)
			{
				yield return 0;

				elapsedTime += Time.deltaTime;
				float progress = elapsedTime / duration;

				card.transform.rotation = Quaternion.Lerp(startingRotation, targetRotation, progress);
			}

			FlipCompleted?.Invoke();
		}

		private bool FlipCard(PlayerRef flipFor, PlayerRef cardPlayer, Action flipCompleted, int gameplayDataID = -1)
		{
			if (!_networkDataManager.PlayerInfos[flipFor].IsConnected || _flipCardCallbacks.ContainsKey(flipFor))
			{
				return false;
			}

			_flipCardCallbacks.Add(flipFor, flipCompleted);
			RPC_FlipCard(flipFor, cardPlayer, gameplayDataID);

			return true;
		}

		public void PutCardBackDown(PlayerRef cardPlayer, bool returnFaceDown, Action PutDownCompleted = null)
		{
			StartCoroutine(PutCardBackDown(_playerCards[cardPlayer], returnFaceDown, Config.MoveToCameraDuration, PutDownCompleted));
		}

		private IEnumerator PutCardBackDown(Card card, bool returnFaceDown, float duration, Action PutDownCompleted = null)
		{
			float elapsedTime = .0f;

			Vector3 startingPosition = card.transform.position;

			Quaternion startingRotation = card.transform.rotation;
			Quaternion targetRotation;

			if (returnFaceDown)
			{
				targetRotation = Quaternion.LookRotation(Vector3.forward, -Vector3.down);
			}
			else
			{
				targetRotation = Quaternion.LookRotation(Vector3.forward, Vector3.down);
			}

			while (elapsedTime < duration)
			{
				yield return 0;

				elapsedTime += Time.deltaTime;
				float progress = elapsedTime / duration;

				card.transform.position = Vector3.Lerp(startingPosition, card.OriginalPosition, progress);
				card.transform.rotation = Quaternion.Lerp(startingRotation, targetRotation, progress);
			}

			if (returnFaceDown)
			{
				card.SetRole(null);
			}

			PutDownCompleted?.Invoke();
		}

		private bool PutCardBackDown(PlayerRef putFor, PlayerRef cardPlayer, bool returnFaceDown, Action putDownCompleted)
		{
			if (!_networkDataManager.PlayerInfos[putFor].IsConnected || _putCardBackDownCallbacks.ContainsKey(putFor))
			{
				return false;
			}

			_putCardBackDownCallbacks.Add(putFor, putDownCompleted);
			RPC_PutCardBackDown(putFor, cardPlayer, returnFaceDown);

			return true;
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		private void RPC_RevealPlayerRole([RpcTarget] PlayerRef player, PlayerRef playerRevealed, int gameplayDataID, bool waitBeforeReveal, bool returnFaceDown)
		{
			Card card = _playerCards[playerRevealed];
			card.SetRole(_gameplayDatabaseManager.GetGameplayData<RoleData>(gameplayDataID));

			StartCoroutine(RevealPlayerRole(card, waitBeforeReveal, returnFaceDown));
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_RevealPlayerRoleFinished(RpcInfo info = default)
		{
			if (!_revealPlayerRoleCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_revealPlayerRoleCallbacks[info.Source](info.Source);
			_revealPlayerRoleCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_MoveCardToCamera([RpcTarget] PlayerRef player, PlayerRef cardPlayer, bool showRevealed, int gameplayDataID = -1)
		{
			if (showRevealed)
			{
				RoleData roleData = _gameplayDatabaseManager.GetGameplayData<RoleData>(gameplayDataID);
				_playerCards[cardPlayer].SetRole(roleData);
			}

			MoveCardToCamera(cardPlayer, showRevealed, () => RPC_MoveCardToCameraFinished());
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_MoveCardToCameraFinished(RpcInfo info = default)
		{
			if (!_moveCardToCameraCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_moveCardToCameraCallbacks[info.Source]();
			_moveCardToCameraCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_FlipCard([RpcTarget] PlayerRef player, PlayerRef cardPlayer, int gameplayDataID = -1)
		{
			FlipCard(cardPlayer, gameplayDataID, () => RPC_FlipCardFinished());
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_FlipCard(PlayerRef cardPlayer, int gameplayDataID = -1)
		{
			FlipCard(cardPlayer, gameplayDataID);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_FlipCardFinished(RpcInfo info = default)
		{
			if (!_flipCardCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_flipCardCallbacks[info.Source]();
			_flipCardCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_PutCardBackDown([RpcTarget] PlayerRef player, PlayerRef cardPlayer, bool returnFaceDown)
		{
			PutCardBackDown(cardPlayer, returnFaceDown, () =>
			{
				if (returnFaceDown)
				{
					_playerCards[cardPlayer].SetRole(null);
				}

				RPC_PutCardBackDownFinished();
			});
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_PutCardBackDownFinished(RpcInfo info = default)
		{
			if (!_putCardBackDownCallbacks.ContainsKey(info.Source))
			{
				return;
			}

			_putCardBackDownCallbacks[info.Source]();
			_putCardBackDownCallbacks.Remove(info.Source);
		}
		#endregion
		#endregion
	}
}