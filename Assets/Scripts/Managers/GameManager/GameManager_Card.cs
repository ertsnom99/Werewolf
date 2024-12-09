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
			float rotationIncrement = 360.0f / _playersOrder.Length;
			Vector3 startingPosition = STARTING_DIRECTION * Config.CardsOffset.Evaluate(_playersOrder.Length);

			int counter = -1;

			foreach (PlayerRef player in _playersOrder)
			{
				counter++;

				Quaternion rotation = Quaternion.Euler(0, rotationIncrement * counter, 0);

				Card card = Instantiate(Config.CardPrefab, rotation * startingPosition, Quaternion.identity);
				card.transform.position += Vector3.up * card.Thickness / 2.0f;

				card.SetOriginalPosition(card.transform.position);
				card.SetPlayer(player);
				card.SetRole(PlayerGameInfos[player].Role);
				card.SetNickname(_networkDataManager.PlayerInfos[player].Nickname);
				card.DetachGroundCanvas();
				card.Flip();

				_playerCards.Add(player, card);

				if (PlayerGameInfos[player].Behaviors.Count <= 0)
				{
					continue;
				}

				foreach (RoleBehavior behavior in PlayerGameInfos[player].Behaviors)
				{
					behavior.transform.position = card.transform.position;
				}
			}
		}
#endif
		private void CreatePlayerCards(PlayerRef[] playersOrder, PlayerRef bottomPlayer, RoleData playerRole)
		{
			int playerCount = playersOrder.Length;

			int counter = -1;
			int rotationOffset = -1;

			float rotationIncrement = 360.0f / playerCount;
			Vector3 startingPosition = STARTING_DIRECTION * Config.CardsOffset.Evaluate(playerCount);

			// Offset the rotation to keep bottomPlayer at the bottom
			foreach (PlayerRef player in playersOrder)
			{
				if (player == bottomPlayer)
				{
					break;
				}

				rotationOffset--;
			}

			// Create cards
			foreach (PlayerRef player in playersOrder)
			{
				counter++;
				rotationOffset++;

				Quaternion rotation = Quaternion.Euler(0, rotationIncrement * rotationOffset, 0);

				Card card = Instantiate(Config.CardPrefab, rotation * startingPosition, Quaternion.identity);
				card.transform.position += Vector3.up * card.Thickness / 2.0f;

				card.SetOriginalPosition(card.transform.position);
				card.SetPlayer(player);
				card.SetNickname(_networkDataManager.PlayerInfos[player].Nickname);
				card.DetachGroundCanvas();

				card.RightClicked += card => { _UIManager.RolesScreen.SelectRole(card.Role, true); };

				if (player == bottomPlayer)
				{
					card.SetRole(playerRole);
					card.Flip();
				}

				_playerCards.Add(player, card);
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
			// Must figure out how many row are necessary
			int rowCount = 0;

			foreach (KeyValuePair<int, int[]> roles in ReservedRoles)
			{
				if (roles.Value.Length <= 0)
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
			foreach (KeyValuePair<int, int[]> roles in ReservedRoles)
			{
				_reservedRolesCards[rowCounter] = new Card[roles.Value.Length];

				Vector3 rowPosition = (Config.ReservedRolesSpacing * rowCounter * Vector3.back) + ((rowCount - 1) * Config.ReservedRolesSpacing * Vector3.forward / 2.0f);

				int columnCounter = 0;

				foreach (int roleGameplayTagID in roles.Value)
				{
					Vector3 columnPosition = (columnCounter * Config.ReservedRolesSpacing * Vector3.right) + ((roles.Value.Length - 1) * Config.ReservedRolesSpacing * Vector3.left / 2.0f);

					Card card = Instantiate(Config.CardPrefab, rowPosition + columnPosition, Quaternion.identity);
					card.transform.position += Vector3.up * card.Thickness / 2.0f;

					card.RightClicked += card => { _UIManager.RolesScreen.SelectRole(card.Role, true); };

					if (roleGameplayTagID > 0)
					{
						RoleData role = _gameplayDatabaseManager.GetGameplayData<RoleData>(roleGameplayTagID);
						card.SetRole(role);
						card.Flip();
					}

					_reservedRolesCards[rowCounter][columnCounter] = card;

					columnCounter++;

					if (columnCounter >= roles.Value.Length)
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