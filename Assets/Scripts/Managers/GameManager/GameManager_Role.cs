using Assets.Scripts.Data.Tags;
using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Gameplay;
using Werewolf.Gameplay.Role;
using Werewolf.UI;

namespace Werewolf.Managers
{
	public partial class GameManager
	{
		private readonly Dictionary<RoleBehavior, RoleData> _unassignedRoleBehaviors = new();

		private readonly Dictionary<int, int[]> ReservedRoles = new();
		private readonly Dictionary<RoleBehavior, IndexedReservedRoles> _reservedRolesByBehavior = new();

		public struct IndexedReservedRoles
		{
			public RoleData[] Roles;
			public RoleBehavior[] Behaviors;
			public int reservedRolesIndex;
		}
#if UNITY_SERVER && UNITY_EDITOR
		private readonly Dictionary<RoleBehavior, Card[]> _reservedCardsByBehavior = new();
#endif
		private readonly Dictionary<PlayerRef, Action<int>> _chooseReservedRoleCallbacks = new();
		private Card[][] _reservedRolesCards;

		private readonly Dictionary<PlayerRef, Action<PlayerRef>> _revealPlayerRoleCallbacks = new();
		private readonly Dictionary<PlayerRef, Action> _moveCardToCameraCallbacks = new();
		private readonly Dictionary<PlayerRef, Action> _flipCardCallbacks = new();
		private readonly Dictionary<PlayerRef, Action> _putCardBackDownCallbacks = new();

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

			PlayerGameInfos[player].Role = roleData;

			if (_networkDataManager.PlayerInfos[player].IsConnected)
			{
				RPC_ChangePlayerCardRole(player, roleData.GameplayTag.CompactTagId);
			}
#if UNITY_SERVER && UNITY_EDITOR
			ChangePlayerCardRole(player, roleData);
#endif
		}

		public void TransferRole(PlayerRef from, PlayerRef to, bool reInitNewBehavior = false)
		{
			RemovePrimaryBehavior(to);

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
						RemoveBehavior(from, behavior);
						AddBehavior(to, behavior, reInitBehavior: reInitNewBehavior);
						break;
					}
				}
			}

			PlayerGameInfos[to].Role = PlayerGameInfos[from].Role;
			PlayerGameInfos[from].Role = null;

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
			RoleData roleData = _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTagID);

			ChangePlayerCardRole(player, roleData);
			_UIManager.RolesScreen.SelectRole(roleData, false);
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

		private void RemovePrimaryBehavior(PlayerRef player)
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
						RemoveBehavior(player, behavior);
						break;
					}
				}
			}
		}

		public void RemoveBehavior(PlayerRef player, RoleBehavior behavior, bool removePlayerFromGroup = true)
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
		public void ReserveRoles(RoleBehavior roleBehavior, RoleData[] rolesData, bool areFaceUp, bool arePrimaryBehavior)
		{
			int[] roles = new int[rolesData.Length];
			RoleBehavior[] behaviors = new RoleBehavior[rolesData.Length];

			for (int i = 0; i < rolesData.Length; i++)
			{
				roles[i] = areFaceUp ? rolesData[i].GameplayTag.CompactTagId : -1;

				foreach (KeyValuePair<RoleBehavior, RoleData> unassignedRoleBehavior in _unassignedRoleBehaviors)
				{
					if (unassignedRoleBehavior.Value == rolesData[i])
					{
						behaviors[i] = unassignedRoleBehavior.Key;
						behaviors[i].SetIsPrimaryBehavior(arePrimaryBehavior);
						_unassignedRoleBehaviors.Remove(unassignedRoleBehavior.Key);
						break;
					}
				}
			}

			int index = _reservedRolesByBehavior.Count;

			ReservedRoles.Add(index, roles);
			_reservedRolesByBehavior.Add(roleBehavior, new() { Roles = rolesData, Behaviors = behaviors, reservedRolesIndex = index });

			RPC_SetReservedRoles(index, roles);
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

			int reservedRolesIndex = _reservedRolesByBehavior[ReservedRoleOwner].reservedRolesIndex;
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
					for (int i = 0; i < ReservedRoles[reservedRolesIndex].Length; i++)
					{
						if (i != specificIndex)
						{
							continue;
						}

						ReservedRoles[reservedRolesIndex][i] = 0;
						break;
					}

					// Check if the entry is now empty and can be removed
					if (!mustRemoveEntry)
					{
						continue;
					}

					for (int i = 0; i < _reservedRolesByBehavior[ReservedRoleOwner].Roles.Length; i++)
					{
						if (_reservedRolesByBehavior[ReservedRoleOwner].Roles[i])
						{
							mustRemoveEntry = false;
							break;
						}
					}
				}
			}
			else
			{
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
			}

			// Update server variable entry
			if (mustRemoveEntry)
			{
				_reservedRolesByBehavior.Remove(ReservedRoleOwner);
#if UNITY_SERVER && UNITY_EDITOR
				_reservedCardsByBehavior.Remove(ReservedRoleOwner);
#endif
				ReservedRoles[reservedRolesIndex] = new int[ReservedRoles[reservedRolesIndex].Length];
			}

			RPC_SetReservedRoles(reservedRolesIndex, ReservedRoles[reservedRolesIndex], true);
		}

		// Returns if there is any reserved roles the player can choose from (will be false if the behavior is already waiting for a callback from this method)
		public bool ChooseReservedRole(RoleBehavior ReservedRoleOwner, int choiceScreenID, bool mustChoose, float maximumDuration, Action<int> callback)
		{
			if (!_networkDataManager.PlayerInfos[ReservedRoleOwner.Player].IsConnected || !_reservedRolesByBehavior.ContainsKey(ReservedRoleOwner) || _chooseReservedRoleCallbacks.ContainsKey(ReservedRoleOwner.Player))
			{
				return false;
			}

			RoleData[] roleDatas = _reservedRolesByBehavior[ReservedRoleOwner].Roles;
			int[] roles = ReservedRoles[_reservedRolesByBehavior[ReservedRoleOwner].reservedRolesIndex];

			for (int i = 0; i < roleDatas.Length; i++)
			{
				if (roleDatas[i])
				{
					roles[i] = roleDatas[i].GameplayTag.CompactTagId;
				}
			}

			_chooseReservedRoleCallbacks.Add(ReservedRoleOwner.Player, callback);
			RPC_ChooseReservedRole(ReservedRoleOwner.Player, roles, choiceScreenID, mustChoose, maximumDuration);

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

			if (_networkDataManager.PlayerInfos[reservedRoleOwner].IsConnected)
			{
				RPC_StopChoosingReservedRole(reservedRoleOwner);
			}
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_SetReservedRoles(int index, int[] reservedRoles, bool updateDisplayedReservedRole = false)
		{
			if (ReservedRoles.ContainsKey(index))
			{
				ReservedRoles[index] = reservedRoles;
			}
			else
			{
				ReservedRoles.Add(index, reservedRoles);
			}

			if (!updateDisplayedReservedRole)
			{
				return;
			}

			for (int i = 0; i < reservedRoles.Length; i++)
			{
				if (reservedRoles[i] == 0 && _reservedRolesCards[index].Length > i && _reservedRolesCards[index][i])
				{
					Destroy(_reservedRolesCards[index][i].gameObject);
				}
			}
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_ChooseReservedRole([RpcTarget] PlayerRef player, int[] roles, int choiceScreenID, bool mustChoose, float maximumDuration)
		{
			List<Choice.ChoiceData> choices = new();

			foreach (int roleGameplayTag in roles)
			{
				if (roleGameplayTag == 0)
				{
					continue;
				}

				RoleData roleData = _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTag);
				choices.Add(new() { Image = roleData.Image, Text = roleData.NameSingular });
			}

			var choiceScreen = _gameplayDatabaseManager.GetGameplayData<ChoiceScreenData>(choiceScreenID);

			if (choiceScreen == null)
			{
				return;
			}

			_UIManager.ChoiceScreen.ConfirmedChoice += GiveReservedRoleChoice;

			_UIManager.ChoiceScreen.Initialize(choices.ToArray(), choiceScreen.ChooseText, choiceScreen.ChoosedText, choiceScreen.DidNotChoosedText, mustChoose, maximumDuration);
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
		private void RPC_StopChoosingReservedRole([RpcTarget] PlayerRef player)
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
				yield return new WaitForSeconds(Config.RoleRevealWaitDuration * GameSpeedModifier);
				yield return FlipCard(card.transform, Config.RoleRevealFlipDuration);
			}

			yield return new WaitForSeconds(Config.RoleRevealHoldDuration * GameSpeedModifier);
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

				card.transform.SetPositionAndRotation(Vector3.Lerp(startingPosition, targetPosition, progress), Quaternion.Lerp(startingRotation, targetRotation, progress));
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

				card.transform.SetPositionAndRotation(Vector3.Lerp(startingPosition, card.OriginalPosition, progress), Quaternion.Lerp(startingRotation, targetRotation, progress));
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
		public void RPC_SetRole([RpcTarget] PlayerRef player, PlayerRef cardPlayer, int gameplayDataID)
		{
			if (gameplayDataID == -1)
			{
				_playerCards[cardPlayer].SetRole(null);
			}
			else
			{
				_playerCards[cardPlayer].SetRole(_gameplayDatabaseManager.GetGameplayData<RoleData>(gameplayDataID));
			}
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