using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using Utilities.GameplayData;
using Werewolf.Data;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;
using static Werewolf.Managers.GameManager;

namespace Werewolf.Gameplay.Role
{
	public class AbominableSectarianBehavior : RoleBehavior
	{
		[Header("Group Creation")]
		[SerializeField]
		private GameHistoryEntryData _createdGroupGameHistoryEntry;

		[SerializeField]
		private TitleScreenData _createdGroupTitleScreen;

		[SerializeField]
		private float _createdGroupTitleDuration;

		[Header("Power Lost")]
		[SerializeField]
		private PlayerGroupData _werewolvesPlayerGroup;

		PlayerRef[] _groupA;
		PlayerRef[] _groupB;
		private bool _hasPower = true;

		private GameManager _gameManager;
		private NetworkDataManager _networkDataManager;
		private GameHistoryManager _gameHistoryManager;

		public override void Initialize()
		{
			base.Initialize();

			_gameManager = GameManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;

			_gameManager.PreChangeGameplayLoopStep += OnPreChangeGameplayLoopStep;
			_gameManager.AddedPlayerToPlayerGroup += OnAddedPlayerToPlayerGroup;
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int priorityIndex, out bool isWakingUp)
		{
			return isWakingUp = false;
		}

		private void OnPreChangeGameplayLoopStep()
		{
			if (Player.IsNone || !CanUsePower)
			{
				return;
			}
			else if (_gameManager.CurrentGameplayLoopStep == GameplayLoopStep.RoleGivenReveal)
			{
				List<KeyValuePair<PlayerRef, NetworkPlayerInfo>> players = _networkDataManager.PlayerInfos.ToList();
				int totalPlayerAmount = players.Count;

				float preRoundedPlayersToAddCount = totalPlayerAmount / 2.0f - 1;
				int playersToAddCount = (int)(Random.Range(0, 2) == 1 ? Mathf.Floor(preRoundedPlayersToAddCount) : Mathf.Ceil(preRoundedPlayersToAddCount));

				_groupA = new PlayerRef[playersToAddCount + 1];
				_groupB = new PlayerRef[totalPlayerAmount - playersToAddCount - 1];
				int groupACount = 1;
				int groupBCount = 0;
				_groupA[0] = Player;

				NetworkGroupDisplay[] networkGroupDisplay = new NetworkGroupDisplay[players.Count];

				while (players.Count > 0)
				{
					int index = Random.Range(0, players.Count);
					PlayerRef player = players[index].Key;

					if (playersToAddCount > 0 && player != Player)
					{
						_gameManager.AddPlayerToPlayerGroup(player, PlayerGroupIDs[1]);
						playersToAddCount--;

						_groupA[groupACount] = player;
						groupACount++;

						networkGroupDisplay[totalPlayerAmount - players.Count] = new NetworkGroupDisplay { Player = player,
																									Background = Color.blue,
																									Text = "A" };
					}
					else if (player == Player)
					{
						networkGroupDisplay[totalPlayerAmount - players.Count] = new NetworkGroupDisplay { Player = player,
																									Background = Color.blue,
																									Text = "A" };
					}
					else
					{

						_groupB[groupBCount] = player;
						groupBCount++;

						networkGroupDisplay[totalPlayerAmount - players.Count] = new NetworkGroupDisplay { Player = player,
																									Background = Color.red,
																									Text = "B" };
					}

					players.Remove(players[index]);
				}

				_gameHistoryManager.AddEntry(_createdGroupGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
												new()
												{
													Name = "AbominableSectarianPlayer",
													Data = _networkDataManager.PlayerInfos[Player].Nickname,
													Type = GameHistorySaveEntryVariableType.Player
												},
												new()
												{
													Name = "GroupANames",
													Data = ConcatenatePlayersNickname(_groupA, _networkDataManager),
													Type = GameHistorySaveEntryVariableType.Players
												},
												new()
												{
													Name = "GroupBNames",
													Data = ConcatenatePlayersNickname(_groupB, _networkDataManager),
													Type = GameHistorySaveEntryVariableType.Players
												}
											});

				_gameManager.RPC_DisplayGroup(networkGroupDisplay);
				_gameManager.WaitForPlayer(Player);

				_gameManager.PreChangeGameplayLoopStep -= OnPreChangeGameplayLoopStep;

				StartCoroutine(DisplayAbominableSectarianCreatedGroupTitle());
			}
		}

		private IEnumerator DisplayAbominableSectarianCreatedGroupTitle()
		{
			_gameManager.RPC_DisplayTitle(_createdGroupTitleScreen.ID.HashCode);
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.DisplayTitle(_createdGroupTitleScreen.ID.HashCode);
#endif
			float UITransitionNormalDuration = _gameManager.GameConfig.UITransitionNormalDuration;

			yield return new WaitForSeconds(UITransitionNormalDuration + _createdGroupTitleDuration * _gameManager.GameSpeedModifier);

			_gameManager.RPC_HideUI();
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.HideUI();
#endif
			yield return new WaitForSeconds(UITransitionNormalDuration);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnAddedPlayerToPlayerGroup(PlayerRef player, UniqueID playerGroupID)
		{
			if (Player == player && playerGroupID == PlayerGroupIDs[1])
			{
				_gameManager.SetPlayerGroupLeader(playerGroupID, Player);
			}
			else if (_hasPower && Player == player && playerGroupID == _werewolvesPlayerGroup.ID)
			{
				_hasPower = false;
				_gameManager.RemovePlayerFromPlayerGroup(Player, PlayerGroupIDs[1]);

				_gameManager.AddedPlayerToPlayerGroup -= OnAddedPlayerToPlayerGroup;
			}
		}

		public override void OnPlayerChanged()
		{
			if (Player.IsNone)
			{
				return;
			}

			// A new player taking this role might be in a different group than the original owner of this role
			if (_groupA != null && _groupB != null)
			{
				if (_groupA.Contains(Player))
				{
					TransferGroups(_groupA, _groupB);
				}
				else
				{
					TransferGroups(_groupB, _groupA);
				}
			}

			void TransferGroups(PlayerRef[] ToAdd, PlayerRef[] ToRemove)
			{
				foreach (PlayerRef player in ToAdd)
				{
					if (_gameManager.PlayerGameInfos[player].IsAlive)
					{
						_gameManager.AddPlayerToPlayerGroup(player, PlayerGroupIDs[1]);
					}
				}

				foreach (PlayerRef player in ToRemove)
				{
					_gameManager.RemovePlayerFromPlayerGroup(player, PlayerGroupIDs[1]);
				}
			}

			bool hadPower = _hasPower;

			_hasPower = !_gameManager.IsPlayerInPlayerGroup(Player, _werewolvesPlayerGroup.ID);

			if (!hadPower && _hasPower)
			{
				_gameManager.AddedPlayerToPlayerGroup += OnAddedPlayerToPlayerGroup;
			}
			else if (!_hasPower)
			{
				_gameManager.RemovePlayerFromPlayerGroup(Player, PlayerGroupIDs[1]);

				if (hadPower)
				{
					_gameManager.AddedPlayerToPlayerGroup -= OnAddedPlayerToPlayerGroup;
				}
			}

			if (_gameManager.IsPlayerInPlayerGroup(Player, PlayerGroupIDs[1]))
			{
				_gameManager.SetPlayerGroupLeader(PlayerGroupIDs[1], Player);
			}
		}

		public override void OnRoleCallDisconnected() { }

		private void OnDestroy()
		{
			_gameManager.PreChangeGameplayLoopStep -= OnPreChangeGameplayLoopStep;
			_gameManager.AddedPlayerToPlayerGroup -= OnAddedPlayerToPlayerGroup;
		}
	}
}