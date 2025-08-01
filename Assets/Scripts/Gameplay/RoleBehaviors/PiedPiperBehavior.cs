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

namespace Werewolf.Gameplay.Role
{
	public class PiedPiperBehavior : RoleBehavior
	{
		[Header("Charm Villagers")]
		[SerializeField]
		private TitleScreenData _lostPowerTitleScreen;

		[SerializeField]
		private TitleScreenData _charmVillagersTitleScreen;

		[SerializeField]
		private float _charmVillagersMaximumDuration;

		[SerializeField]
		private GameHistoryEntryData _charmedVillagersGameHistoryEntry;

		[SerializeField]
		private float _showCharmedVillagersHighlightHoldDuration;

		[Header("Show Charmed Villagers")]
		[SerializeField]
		private float _showAllCharmedVillagersHighlightHoldDuration;

		[SerializeField]
		private TitleScreenData _charmedVillagersTitleScreen;

		[SerializeField]
		private TitleScreenData _charmedVillagersRecognizingEachOtherTitleScreen;

		[Header("Power Lost")]
		[SerializeField]
		private PlayerGroupData _werewolvesPlayerGroup;

		[SerializeField]
		private GameHistoryEntryData _lostPowerGameHistoryEntry;

		private bool _hasPower = true;
		private IEnumerator _endCharmVillagersAfterTimeCoroutine;
		private IEnumerator _highlightCharmedVillagersCoroutine;

		private GameManager _gameManager;
		private GameHistoryManager _gameHistoryManager;
		private NetworkDataManager _networkDataManager;

		public override void Initialize()
		{
			base.Initialize();

			_gameManager = GameManager.Instance;
			_gameHistoryManager = GameHistoryManager.Instance;
			_networkDataManager = NetworkDataManager.Instance;

			_gameManager.AddedPlayerToPlayerGroup += OnAddedPlayerToPlayerGroup;

			if (PlayerGroupIDs.Count < 2)
			{
				Debug.LogError($"{nameof(PiedPiperBehavior)} must have two player groups: the first one for the villager and the second one for himself and the charmed villagers");
			}

			if (NightPriorities.Count < 2)
			{
				Debug.LogError($"{nameof(PiedPiperBehavior)} must have two night priorities: the first one to charm villagers and the second one to let charmed villagers know each other");
			}
		}

		public override void OnSelectedToDistribute(List<RoleSetup> mandatoryRoles, List<RoleSetup> availableRoles, List<RoleData> rolesToDistribute) { }

		public override bool OnRoleCall(int priorityIndex, out bool isWakingUp)
		{
			if (priorityIndex == NightPriorities[0].index)
			{
				if (_hasPower)
				{
					isWakingUp = CharmVillagers();
				}
				else
				{
					StartCoroutine(ShowLostPower());
					isWakingUp = false;
				}

				return true;
			}
			else if (priorityIndex == NightPriorities[1].index)
			{
				StartCoroutine(ShowCharmedVillagers());

				isWakingUp = false;
				return true;
			}

			return isWakingUp = false;
		}

		private bool CharmVillagers()
		{
			List<PlayerRef> notCharmedVillagers = _gameManager.GetAlivePlayers();
			HashSet<PlayerRef> charmedVillagers = _gameManager.GetPlayersFromPlayerGroup(PlayerGroupIDs[1]);

			foreach (PlayerRef villager in charmedVillagers)
			{
				notCharmedVillagers.Remove(villager);
			}

			if (!_gameManager.SelectPlayers(Player,
											notCharmedVillagers,
											_charmVillagersTitleScreen.ID.HashCode,
											_charmVillagersMaximumDuration * _gameManager.GameSpeedModifier,
											false,
											2,
											ChoicePurpose.Other,
											OnVillagersSelected))
			{
				StartCoroutine(WaitToStopWaitingForPlayer());
				return false;
			}

			_endCharmVillagersAfterTimeCoroutine = EndCharmVillagersAfterTime();
			StartCoroutine(_endCharmVillagersAfterTimeCoroutine);

			return true;
		}

		private IEnumerator WaitToStopWaitingForPlayer()
		{
			yield return 0;
			_gameManager.StopWaintingForPlayer(Player);
		}

		private void OnVillagersSelected(PlayerRef[] players)
		{
			if (_endCharmVillagersAfterTimeCoroutine != null)
			{
				StopCoroutine(_endCharmVillagersAfterTimeCoroutine);
				_endCharmVillagersAfterTimeCoroutine = null;
			}

			foreach (PlayerRef player in players)
			{
				_gameManager.AddPlayerToPlayerGroup(player, PlayerGroupIDs[1]);
			}

			_gameHistoryManager.AddEntry(_charmedVillagersGameHistoryEntry.ID,
										new GameHistorySaveEntryVariable[] {
										new()
										{
											Name = "PiedPiperPlayer",
											Data = _networkDataManager.PlayerInfos[Player].Nickname,
											Type = GameHistorySaveEntryVariableType.Player
										},
										new()
										{
											Name = "CharmedPlayers",
											Data = ConcatenatePlayersNickname(players, _networkDataManager),
											Type = GameHistorySaveEntryVariableType.Players
										}
										});

			_highlightCharmedVillagersCoroutine = HighlightCharmedVillagers(players, new PlayerRef[] { Player }, _showCharmedVillagersHighlightHoldDuration * _gameManager.GameSpeedModifier);
			StartCoroutine(_highlightCharmedVillagersCoroutine);
		}

		private IEnumerator EndCharmVillagersAfterTime()
		{
			float timeLeft = _charmVillagersMaximumDuration * _gameManager.GameSpeedModifier;

			while (timeLeft > 0)
			{
				yield return 0;
				timeLeft -= Time.deltaTime;
			}

			_endCharmVillagersAfterTimeCoroutine = null;
			_gameManager.StopSelectingPlayers(Player);

			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator ShowLostPower()
		{
			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_DisplayTitle(Player, _lostPowerTitleScreen.ID.HashCode);
			}

			yield return 0;

			_gameManager.StopWaintingForPlayer(Player);
		}

		private IEnumerator ShowCharmedVillagers()
		{
			_gameManager.StartWaitingForPlayersRollCall += OnStartWaitingForPlayersRollCall;

			if (_networkDataManager.PlayerInfos[Player].IsConnected)
			{
				_gameManager.RPC_DisplayTitle(Player, _charmedVillagersRecognizingEachOtherTitleScreen.ID.HashCode);
			}

			HashSet<PlayerRef> charmedVillagers = _gameManager.GetPlayersFromPlayerGroup(PlayerGroupIDs[1]);
			charmedVillagers.Remove(Player);
			PlayerRef[] charmedVillagersArray = charmedVillagers.ToArray();

			yield return StartCoroutine(HighlightCharmedVillagers(charmedVillagersArray, charmedVillagersArray, _showAllCharmedVillagersHighlightHoldDuration * _gameManager.GameSpeedModifier));

			_gameManager.StartWaitingForPlayersRollCall -= OnStartWaitingForPlayersRollCall;
		}

		private void OnStartWaitingForPlayersRollCall()
		{
			HashSet<PlayerRef> charmedVillagers = _gameManager.GetPlayersFromPlayerGroup(PlayerGroupIDs[1]);

			foreach (PlayerRef charmedVillager in charmedVillagers)
			{
				if (charmedVillager != Player)
				{
					_gameManager.SetPlayerAwake(charmedVillager, true);
				}
			}
		}

		private IEnumerator HighlightCharmedVillagers(PlayerRef[] charmedVillagers, PlayerRef[] highlightedFor, float duration)
		{
			foreach (PlayerRef player in highlightedFor)
			{
				if (_networkDataManager.PlayerInfos[player].IsConnected)
				{
					_gameManager.RPC_SetPlayersCardHighlightVisible(player, charmedVillagers, true);
				}
			}
#if UNITY_SERVER && UNITY_EDITOR
			_gameManager.SetPlayersCardHighlightVisible(charmedVillagers, true);
#endif
			yield return new WaitForSeconds(duration);

			_gameManager.StopWaintingForPlayer(Player);
		}

		public override void GetTitlesOverride(int priorityIndex, ref Dictionary<PlayerRef, int> titlesOverride)
		{
			if (priorityIndex != NightPriorities[1].index)
			{
				return;
			}

			titlesOverride.Clear();

			foreach (KeyValuePair<PlayerRef, PlayerGameInfo> playerInfo in _gameManager.PlayerGameInfos)
			{
				if (_gameManager.IsPlayerInPlayerGroup(playerInfo.Key, PlayerGroupIDs[1]))
				{
					titlesOverride.Add(playerInfo.Key, _charmedVillagersTitleScreen.ID.HashCode);
				}
				else
				{
					titlesOverride.Add(playerInfo.Key, _charmedVillagersRecognizingEachOtherTitleScreen.ID.HashCode);
				}
			}
		}

		private void OnAddedPlayerToPlayerGroup(PlayerRef player, UniqueID playerGroupID)
		{
			if (Player == player && playerGroupID == PlayerGroupIDs[1])
			{
				_gameManager.SetPlayerGroupLeader(playerGroupID, Player);
			}
			else if (_hasPower && Player == player && playerGroupID == _werewolvesPlayerGroup.ID)
			{
				_gameHistoryManager.AddEntry(_lostPowerGameHistoryEntry.ID,
											new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "PiedPiperPlayer",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
											});

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
			_gameManager.AddedPlayerToPlayerGroup -= OnAddedPlayerToPlayerGroup;
		}
	}
}
