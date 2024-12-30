using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Werewolf.Data;
using Werewolf.Gameplay;
using Werewolf.Gameplay.Role;

namespace Werewolf.Managers
{
	public partial class GameManager
	{
		[SerializeField]
		public SplineContainer _cardPlacementSpline;

		private readonly Dictionary<PlayerRef, Card> _playerCards = new();

		#region Create Card
#if UNITY_SERVER && UNITY_EDITOR
		private void CreatePlayerCardsForServer()
		{
			for (int i = 0; i < _playersOrder.Length; i++)
			{
				Card card = Instantiate(Config.CardPrefab, _cardPlacementSpline.EvaluatePosition((float)i / _playersOrder.Length), Quaternion.identity);
				card.transform.position += Vector3.up * card.Thickness / 2.0f;

				card.SetOriginalPosition(card.transform.position);
				card.SetPlayer(_playersOrder[i]);
				card.SetRole(PlayerGameInfos[_playersOrder[i]].Role);
				card.SetNickname(_networkDataManager.PlayerInfos[_playersOrder[i]].Nickname);
				card.DetachGroundCanvas();
				card.Flip();

				_playerCards.Add(_playersOrder[i], card);

				if (PlayerGameInfos[_playersOrder[i]].Behaviors.Count > 0)
				{
					foreach (RoleBehavior behavior in PlayerGameInfos[_playersOrder[i]].Behaviors)
					{
						behavior.transform.position = card.transform.position;
					}
				}
			}
		}
#endif
		private void CreatePlayerCards(PlayerRef[] playersOrder, PlayerRef bottomPlayer, RoleData playerRole)
		{
			// Calculate the index necessary to offset all cards to place the bottom player card at the bottom
			int playerAmount = playersOrder.Length;
			int indexOffset = 0;

			if (playersOrder[0] != bottomPlayer)
			{
				for (int i = playerAmount - 1; i >= 0; i--)
				{
					indexOffset++;

					if (playersOrder[i] == bottomPlayer)
					{
						break;
					}
				}
			}

			// Create all player cards
			for (int i = 0; i < playerAmount; i++)
			{
				Card card = Instantiate(Config.CardPrefab, _cardPlacementSpline.EvaluatePosition((float)((i + indexOffset) % playerAmount) / playerAmount), Quaternion.identity);
				card.transform.position += Vector3.up * card.Thickness / 2.0f;

				card.SetOriginalPosition(card.transform.position);
				card.SetPlayer(playersOrder[i]);
				card.SetNickname(_networkDataManager.PlayerInfos[playersOrder[i]].Nickname);
				card.DetachGroundCanvas();

				card.RightClicked += card => { _UIManager.RolesScreen.SelectRole(card.Role, true); };

				if (playersOrder[i] == bottomPlayer)
				{
					card.SetRole(playerRole);
					card.Flip();
				}

				_playerCards.Add(playersOrder[i], card);
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