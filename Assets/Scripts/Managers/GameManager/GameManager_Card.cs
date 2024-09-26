using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Werewolf.Data;

namespace Werewolf
{
	public partial class GameManager
	{
		private readonly Dictionary<PlayerRef, Card> _playerCards = new();

		private readonly Vector3 STARTING_DIRECTION = Vector3.back;

		#region Create Card
#if UNITY_SERVER && UNITY_EDITOR
		private void CreatePlayerCardsForServer()
		{
			float rotationIncrement = 360.0f / PlayerGameInfos.Count;
			Vector3 startingPosition = STARTING_DIRECTION * Config.CardsOffset.Evaluate(PlayerGameInfos.Count);

			int counter = -1;

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in PlayerGameInfos)
			{
				counter++;

				Quaternion rotation = Quaternion.Euler(0, rotationIncrement * counter, 0);

				Card card = Instantiate(Config.CardPrefab, rotation * startingPosition, Quaternion.identity);
				card.transform.position += Vector3.up * card.Thickness / 2.0f;

				card.SetOriginalPosition(card.transform.position);
				card.SetPlayer(playerInfo.Key);
				card.SetRole(playerInfo.Value.Role);
				card.SetNickname(_networkDataManager.PlayerInfos[playerInfo.Key].Nickname);
				card.DetachGroundCanvas();
				card.Flip();

				_playerCards.Add(playerInfo.Key, card);

				if (playerInfo.Value.Behaviors.Count <= 0)
				{
					continue;
				}

				foreach (RoleBehavior behavior in playerInfo.Value.Behaviors)
				{
					behavior.transform.position = card.transform.position;
				}
			}
		}
#endif
		private void CreatePlayerCards(PlayerRef bottomPlayer, RoleData playerRole)
		{
			NetworkDictionary<PlayerRef, Network.PlayerNetworkInfo> playerInfos = _networkDataManager.PlayerInfos;
			int playerCount = playerInfos.Count;

			int counter = -1;
			int rotationOffset = -1;

			float rotationIncrement = 360.0f / playerCount;
			Vector3 startingPosition = STARTING_DIRECTION * Config.CardsOffset.Evaluate(playerCount);

			// Offset the rotation to keep bottomPlayer at the bottom
			foreach (KeyValuePair<PlayerRef, Network.PlayerNetworkInfo> playerInfo in playerInfos)
			{
				if (playerInfo.Key == bottomPlayer)
				{
					break;
				}

				rotationOffset--;
			}

			// Create cards
			foreach (KeyValuePair<PlayerRef, Network.PlayerNetworkInfo> playerInfo in playerInfos)
			{
				counter++;
				rotationOffset++;

				Quaternion rotation = Quaternion.Euler(0, rotationIncrement * rotationOffset, 0);

				Card card = Instantiate(Config.CardPrefab, rotation * startingPosition, Quaternion.identity);
				card.transform.position += Vector3.up * card.Thickness / 2.0f;

				card.SetOriginalPosition(card.transform.position);
				card.SetPlayer(playerInfo.Key);
				card.SetNickname(playerInfo.Value.Nickname);
				card.DetachGroundCanvas();

				card.RightClicked += card => { _UIManager.RolesScreen.SelectRole(card.Role); };

				if (playerInfo.Key == bottomPlayer)
				{
					card.SetRole(playerRole);
					card.Flip();
				}

				_playerCards.Add(playerInfo.Key, card);
			}
		}

#if UNITY_SERVER && UNITY_EDITOR
		private void CreateReservedRoleCardsForServer()
		{
			int rowCounter = 0;

			foreach (KeyValuePair<RoleBehavior, IndexedReservedRoles> reservedRoleByBehavior in _reservedRolesByBehavior)
			{
				Vector3 rowPosition = (Config.ReservedRolesSpacing * rowCounter * Vector3.back) + ((_reservedRolesByBehavior.Count - 1) * Config.ReservedRolesSpacing * Vector3.forward / 2.0f);
				Card[] cards = new Card[reservedRoleByBehavior.Value.Roles.Length];

				int columnCounter = 0;

				foreach (RoleData role in reservedRoleByBehavior.Value.Roles)
				{
					Vector3 columnPosition = (columnCounter * Config.ReservedRolesSpacing * Vector3.right) + ((reservedRoleByBehavior.Value.Roles.Length - 1) * Config.ReservedRolesSpacing * Vector3.left / 2.0f);

					Card card = Instantiate(Config.CardPrefab, rowPosition + columnPosition, Quaternion.identity);
					card.transform.position += Vector3.up * card.Thickness / 2.0f;

					card.SetRole(role);
					card.Flip();

					cards[columnCounter] = card;

					columnCounter++;
				}

				_reservedCardsByBehavior.Add(reservedRoleByBehavior.Key, cards);
				rowCounter++;
			}
		}
#endif
		private void CreateReservedRoleCards()
		{
			// Must figure out how many actual row are in the networked data
			int rowCount = 0;

			foreach (RolesContainer rolesContainer in ReservedRoles)
			{
				if (rolesContainer.RoleCount <= 0)
				{
					break;
				}

				rowCount++;
			}

			_reservedRolesCards = new Card[rowCount][];

			if (rowCount <= 0)
			{
				return;
			}

			int rowCounter = 0;

			// Create the reserved cards
			foreach (RolesContainer reservedRole in ReservedRoles)
			{
				_reservedRolesCards[rowCounter] = new Card[reservedRole.RoleCount];

				Vector3 rowPosition = (Config.ReservedRolesSpacing * rowCounter * Vector3.back) + ((rowCount - 1) * Config.ReservedRolesSpacing * Vector3.forward / 2.0f);

				int columnCounter = 0;

				foreach (int roleGameplayTagID in reservedRole.Roles)
				{
					Vector3 columnPosition = (columnCounter * Config.ReservedRolesSpacing * Vector3.right) + ((reservedRole.RoleCount - 1) * Config.ReservedRolesSpacing * Vector3.left / 2.0f);

					Card card = Instantiate(Config.CardPrefab, rowPosition + columnPosition, Quaternion.identity);
					card.transform.position += Vector3.up * card.Thickness / 2.0f;

					card.RightClicked += card => { _UIManager.RolesScreen.SelectRole(card.Role); };

					if (roleGameplayTagID > 0)
					{
						RoleData role = _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTagID);
						card.SetRole(role);
						card.Flip();
					}

					_reservedRolesCards[rowCounter][columnCounter] = card;

					columnCounter++;

					if (columnCounter >= reservedRole.RoleCount)
					{
						break;
					}
				}

				rowCounter++;

				if (rowCounter >= rowCount)
				{
					break;
				}
			}
		}
		#endregion

		#region Destroy Card
		public void DestroyPlayerCard(PlayerRef cardPlayer)
		{
			Destroy(_playerCards[cardPlayer].gameObject);
		}

		#region RPC Calls
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.Proxies, Channel = RpcChannel.Reliable)]
		public void RPC_DestroyPlayerCard([RpcTarget] PlayerRef player, PlayerRef cardPlayer)
		{
			DestroyPlayerCard(cardPlayer);
		}
		#endregion
		#endregion
	}
}