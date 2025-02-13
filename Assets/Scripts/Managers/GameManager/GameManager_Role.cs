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
				foreach (PlayerGroupData playerGroup in roleData.PlayerGroups)
				{
					AddPlayerToPlayerGroup(player, playerGroup.ID);
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
				RPC_ChangePlayerCardRole(player, roleData.ID.HashCode);
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
				foreach (PlayerGroupData playerGroup in PlayerGameInfos[from].Role.PlayerGroups)
				{
					AddPlayerToPlayerGroup(to, playerGroup.ID);
				}
			}
			else
			{
				foreach (RoleBehavior behavior in PlayerGameInfos[from].Behaviors)
				{
					if (behavior.IsPrimaryBehavior)
					{
						RemoveBehavior(from, behavior);
						AddBehavior(to, behavior);
						break;
					}
				}
			}

			PlayerGameInfos[to].Role = PlayerGameInfos[from].Role;
			PlayerGameInfos[from].Role = null;

			if (_networkDataManager.PlayerInfos[to].IsConnected)
			{
				RPC_ChangePlayerCardRole(to, PlayerGameInfos[to].Role.ID.HashCode);
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
		private void RPC_ChangePlayerCardRole([RpcTarget] PlayerRef player, int roleID)
		{
			if (!_gameplayDataManager.TryGetGameplayData(roleID, out RoleData roleData))
			{
				Debug.LogError($"Could not find the role {roleID}");
			}

			ChangePlayerCardRole(player, roleData);
			_UIManager.RolesScreen.SelectRole(roleData, false);
		}
		#endregion
		#endregion

		#region Behavior Change
		public void AddBehavior(PlayerRef player, RoleBehavior behavior, bool addPlayerToPlayerGroup = true)
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
				foreach (UniqueID playerGroupID in behavior.GetCurrentPlayerGroupIDs())
				{
					AddPlayerToPlayerGroup(player, playerGroupID);
				}
			}

			PlayerGameInfos[player].Behaviors.Add(behavior);
			behavior.SetPlayer(player);

#if UNITY_SERVER && UNITY_EDITOR
			behavior.transform.position = _playerCards[player].transform.position;
#endif
		}

		private void RemovePrimaryBehavior(PlayerRef player)
		{
			if (PlayerGameInfos[player].Behaviors.Count <= 0)
			{
				foreach (PlayerGroupData playerGroup in PlayerGameInfos[player].Role.PlayerGroups)
				{
					RemovePlayerFromPlayerGroup(player, playerGroup.ID);
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
				foreach (UniqueID playerGroupID in behavior.GetCurrentPlayerGroupIDs())
				{
					RemovePlayerFromPlayerGroup(player, playerGroupID);
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
			int[] roleIDs = new int[rolesData.Length];
			RoleBehavior[] behaviors = new RoleBehavior[rolesData.Length];

			for (int i = 0; i < rolesData.Length; i++)
			{
				roleIDs[i] = areFaceUp ? rolesData[i].ID.HashCode : -1;

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

			ReservedRoles.Add(index, roleIDs);
			_reservedRolesByBehavior.Add(roleBehavior, new() { Roles = rolesData, Behaviors = behaviors, reservedRolesIndex = index });

			RPC_SetReservedRoles(index, roleIDs);
		}

		public IndexedReservedRoles GetReservedRoles(RoleBehavior roleBehavior)
		{
			_reservedRolesByBehavior.TryGetValue(roleBehavior, out IndexedReservedRoles roles);
			return roles;
		}

		public void RemoveReservedRoles(RoleBehavior ReservedRoleOwner, int[] specificIndexes)
		{
			if (!_reservedRolesByBehavior.TryGetValue(ReservedRoleOwner, out IndexedReservedRoles reservedRoles))
			{
				return;
			}

			int reservedRolesIndex = reservedRoles.reservedRolesIndex;
			bool mustRemoveEntry = true;

			if (specificIndexes.Length > 0)
			{
				foreach (int specificIndex in specificIndexes)
				{
					RoleBehavior behavior = reservedRoles.Behaviors[specificIndex];

					if (behavior && behavior.Player == null)
					{
						Destroy(behavior.gameObject);
					}

					reservedRoles.Roles[specificIndex] = null;
					reservedRoles.Behaviors[specificIndex] = null;
#if UNITY_SERVER && UNITY_EDITOR
					Card[] reservedCards = _reservedCardsByBehavior[ReservedRoleOwner];

					if (reservedCards[specificIndex])
					{
						Destroy(reservedCards[specificIndex].gameObject);
					}

					reservedCards[specificIndex] = null;
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

					for (int i = 0; i < reservedRoles.Roles.Length; i++)
					{
						if (reservedRoles.Roles[i])
						{
							mustRemoveEntry = false;
							break;
						}
					}
				}
			}
			else
			{
				for (int i = 0; i < reservedRoles.Roles.Length; i++)
				{
					RoleBehavior behavior = reservedRoles.Behaviors[i];

					if (behavior && behavior.Player == null)
					{
						Destroy(behavior.gameObject);
					}
#if UNITY_SERVER && UNITY_EDITOR
					Card[] reservedCards = _reservedCardsByBehavior[ReservedRoleOwner];

					if (reservedCards[i])
					{
						Destroy(reservedCards[i].gameObject);
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
			if (!_networkDataManager.PlayerInfos[ReservedRoleOwner.Player].IsConnected || !_reservedRolesByBehavior.TryGetValue(ReservedRoleOwner, out IndexedReservedRoles reservedRoles) || _chooseReservedRoleCallbacks.ContainsKey(ReservedRoleOwner.Player))
			{
				return false;
			}

			RoleData[] roleDatas = reservedRoles.Roles;
			int[] roleIDs = ReservedRoles[reservedRoles.reservedRolesIndex];

			for (int i = 0; i < roleDatas.Length; i++)
			{
				if (roleDatas[i])
				{
					roleIDs[i] = roleDatas[i].ID.HashCode;
				}
			}

			_chooseReservedRoleCallbacks.Add(ReservedRoleOwner.Player, callback);
			RPC_ChooseReservedRole(ReservedRoleOwner.Player, roleIDs, choiceScreenID, mustChoose, maximumDuration);

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
		public void RPC_ChooseReservedRole([RpcTarget] PlayerRef player, int[] roleIDs, int choiceScreenID, bool mustChoose, float maximumDuration)
		{
			List<Choice.ChoiceData> choices = new();

			foreach (int roleID in roleIDs)
			{
				if (roleID == 0)
				{
					continue;
				}

				if (!_gameplayDataManager.TryGetGameplayData(roleID, out RoleData roleData))
				{
					Debug.LogError($"Could not find the role {roleID}");
				}

				choices.Add(new() { Image = roleData.Image, Text = roleData.NameSingular });
			}

			if (!_gameplayDataManager.TryGetGameplayData(choiceScreenID, out ChoiceScreenData choiceScreen))
			{
				Debug.LogError($"Could not find the choice screen {choiceScreenID}");
				return;
			}

			_UIManager.ChoiceScreen.ConfirmedChoice += GiveReservedRoleChoice;

			_UIManager.ChoiceScreen.Initialize(choices.ToArray(), choiceScreen.ChooseText, choiceScreen.ChoosedText, choiceScreen.DidNotChoosedText, mustChoose, maximumDuration);
			_UIManager.FadeIn(_UIManager.ChoiceScreen, Config.UITransitionNormalDuration);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		public void RPC_GiveReservedRoleChoice(int choiceIndex, RpcInfo info = default)
		{
			if (!_chooseReservedRoleCallbacks.TryGetValue(info.Source, out Action<int> callback))
			{
				return;
			}

			callback(choiceIndex);
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
			RPC_RevealPlayerRole(revealTo, playerRevealed, PlayerGameInfos[playerRevealed].Role.ID.HashCode, waitBeforeReveal, returnFaceDown);

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

		private bool MoveCardToCamera(PlayerRef movedFor, PlayerRef cardPlayer, bool showRevealed, int roleID, Action movementCompleted)
		{
			if (!_networkDataManager.PlayerInfos[movedFor].IsConnected || _moveCardToCameraCallbacks.ContainsKey(movedFor))
			{
				return false;
			}

			_moveCardToCameraCallbacks.Add(movedFor, movementCompleted);
			RPC_MoveCardToCamera(movedFor, cardPlayer, showRevealed, roleID);

			return true;
		}

		public void FlipCard(PlayerRef cardPlayer, int roleID = -1, Action FlipCompleted = null)
		{
			if (roleID == -1)
			{
				_playerCards[cardPlayer].SetRole(null);
			}
			else
			{
				if (!_gameplayDataManager.TryGetGameplayData(roleID, out RoleData roleData))
				{
					Debug.LogError($"Could not find the role {roleID}");
				}

				_playerCards[cardPlayer].SetRole(roleData);
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

		private bool FlipCard(PlayerRef flipFor, PlayerRef cardPlayer, Action flipCompleted, int roleID = -1)
		{
			if (!_networkDataManager.PlayerInfos[flipFor].IsConnected || _flipCardCallbacks.ContainsKey(flipFor))
			{
				return false;
			}

			_flipCardCallbacks.Add(flipFor, flipCompleted);
			RPC_FlipCard(flipFor, cardPlayer, roleID);

			return true;
		}

		public void PutCardBackDown(PlayerRef cardPlayer, bool returnFaceDown, Action PutDownCompleted = null)
		{
			StartCoroutine(PutCardBackDown(_playerCards[cardPlayer], returnFaceDown, Config.MoveToCameraDuration, PutDownCompleted));
		}

		private IEnumerator PutCardBackDown(Card card, bool returnFaceDown, float duration, Action PutDownCompleted = null)
		{
			float elapsedTime = .0f;

			card.transform.GetPositionAndRotation(out Vector3 startingPosition, out Quaternion startingRotation);
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
		private void RPC_RevealPlayerRole([RpcTarget] PlayerRef player, PlayerRef playerRevealed, int roleID, bool waitBeforeReveal, bool returnFaceDown)
		{
			if (!_gameplayDataManager.TryGetGameplayData(roleID, out RoleData roleData))
			{
				Debug.LogError($"Could not find the role {roleID}");
			}

			Card card = _playerCards[playerRevealed];
			card.SetRole(roleData);

			StartCoroutine(RevealPlayerRole(card, waitBeforeReveal, returnFaceDown));
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_RevealPlayerRoleFinished(RpcInfo info = default)
		{
			if (!_revealPlayerRoleCallbacks.TryGetValue(info.Source, out Action<PlayerRef> callback))
			{
				return;
			}

			callback(info.Source);
			_revealPlayerRoleCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_MoveCardToCamera([RpcTarget] PlayerRef player, PlayerRef cardPlayer, bool showRevealed, int roleID = -1)
		{
			if (showRevealed)
			{
				if (!_gameplayDataManager.TryGetGameplayData(roleID, out RoleData roleData))
				{
					Debug.LogError($"Could not find the role {roleID}");
				}

				_playerCards[cardPlayer].SetRole(roleData);
			}

			MoveCardToCamera(cardPlayer, showRevealed, () => RPC_MoveCardToCameraFinished());
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_MoveCardToCameraFinished(RpcInfo info = default)
		{
			if (!_moveCardToCameraCallbacks.TryGetValue(info.Source, out Action callback))
			{
				return;
			}

			callback();
			_moveCardToCameraCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_FlipCard([RpcTarget] PlayerRef player, PlayerRef cardPlayer, int roleID = -1)
		{
			FlipCard(cardPlayer, roleID, () => RPC_FlipCardFinished());
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_FlipCard(PlayerRef cardPlayer, int roleID = -1)
		{
			FlipCard(cardPlayer, roleID);
		}

		[Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
		private void RPC_FlipCardFinished(RpcInfo info = default)
		{
			if (!_flipCardCallbacks.TryGetValue(info.Source, out Action callback))
			{
				return;
			}

			callback();
			_flipCardCallbacks.Remove(info.Source);
		}

		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_SetRole([RpcTarget] PlayerRef player, PlayerRef cardPlayer, int roleID)
		{
			if (roleID == -1)
			{
				_playerCards[cardPlayer].SetRole(null);
			}
			else
			{
				if (!_gameplayDataManager.TryGetGameplayData(roleID, out RoleData roleData))
				{
					Debug.LogError($"Could not find the role {roleID}");
				}

				_playerCards[cardPlayer].SetRole(roleData);
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
			if (!_putCardBackDownCallbacks.TryGetValue(info.Source, out var callback))
			{
				return;
			}

			callback();
			_putCardBackDownCallbacks.Remove(info.Source);
		}
		#endregion
		#endregion
	}
}