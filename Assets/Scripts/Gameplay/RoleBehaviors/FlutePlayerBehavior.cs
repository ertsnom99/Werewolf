using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Data.Tags;
using Fusion;
using UnityEngine;
using Werewolf.Data;
using Werewolf.Gameplay;
using Werewolf.Gameplay.Role;
using Werewolf.Managers;
using Werewolf.Network;
using static Werewolf.Managers.GameHistoryManager;

public class FlutePlayerBehavior : RoleBehavior
{
	[Header("Charm Villagers")]
	[SerializeField]
	private GameplayTag _lostPowerImage;

	[SerializeField]
	private GameplayTag _charmVillagersImage;

	[SerializeField]
	private float _charmVillagersMaximumDuration;

	[SerializeField]
	private GameplayTag _charmedVillagersGameHistoryEntry;

	[SerializeField]
	private float _showCharmedVillagersHighlightHoldDuration;

	[Header("Show Charmed Villagers")]
	[SerializeField]
	private float _showAllCharmedVillagersHighlightHoldDuration;

	[SerializeField]
	private GameplayTag _charmedVillagersImage;

	[SerializeField]
	private GameplayTag _charmedVillagersRecognizingEachOtherImage;

	[Header("Power Lost")]
	[SerializeField]
	private GameplayTag _werewolvesPlayerGroup;

	[SerializeField]
	private GameplayTag _lostPowerGameHistoryEntry;

	private bool _hasPower = true;

	private IEnumerator _endCharmVillagersAfterTimeCoroutine;
	private IEnumerator _highlightCharmedVillagersCoroutine;

	private GameManager _gameManager;
	private GameHistoryManager _gameHistoryManager;
	private NetworkDataManager _networkDataManager;

	public override void Initialize()
	{
		_gameManager = GameManager.Instance;
		_gameHistoryManager = GameHistoryManager.Instance;
		_networkDataManager = NetworkDataManager.Instance;

		_gameManager.AddedPlayerToPlayerGroup += OnAddedPlayerToPlayerGroup;

		if (PlayerGroups.Count < 2)
		{
			Debug.LogError($"{nameof(FlutePlayerBehavior)} must have two player groups: the first one for the villager and the second one for himself and the charmed villagers");
		}

		if (NightPriorities.Count < 2)
		{
			Debug.LogError($"{nameof(FlutePlayerBehavior)} must have two night priorities: the first one to charm villagers and the second one to let charmed villagers know each other");
		}
	}

	public override void OnSelectedToDistribute(List<RoleSetupData> mandatoryRoles, List<RoleSetupData> availableRoles, List<RoleData> rolesToDistribute) { }

	public override bool OnRoleCall(int nightCount, int priorityIndex, out bool isWakingUp)
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
		HashSet<PlayerRef> charmedVillagers = _gameManager.GetPlayersFromPlayerGroup(PlayerGroups[1]);

		foreach (PlayerRef villager in charmedVillagers)
		{
			notCharmedVillagers.Remove(villager);
		}

		if (!_gameManager.SelectPlayers(Player,
										notCharmedVillagers,
										_charmVillagersImage.CompactTagId,
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

		foreach(PlayerRef player in players)
		{
			_gameManager.AddPlayerToPlayerGroup(player, PlayerGroups[1]);
		}

		_gameHistoryManager.AddEntry(_charmedVillagersGameHistoryEntry,
									new GameHistorySaveEntryVariable[] {
										new()
										{
											Name = "FlutePlayerPlayer",
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
			_gameManager.RPC_DisplayTitle(Player, _lostPowerImage.CompactTagId);
		}

		yield return 0;
		
		_gameManager.StopWaintingForPlayer(Player);
	}

	private IEnumerator ShowCharmedVillagers()
	{
		_gameManager.StartWaitingForPlayersRollCall += OnStartWaitingForPlayersRollCall;

		if (_networkDataManager.PlayerInfos[Player].IsConnected)
		{
			_gameManager.RPC_DisplayTitle(Player, _charmedVillagersRecognizingEachOtherImage.CompactTagId);
		}

		HashSet<PlayerRef> charmedVillagers = _gameManager.GetPlayersFromPlayerGroup(PlayerGroups[1]);
		charmedVillagers.Remove(Player);
		PlayerRef[] charmedVillagersArray = charmedVillagers.ToArray();

		yield return StartCoroutine(HighlightCharmedVillagers(charmedVillagersArray, charmedVillagersArray, _showAllCharmedVillagersHighlightHoldDuration * _gameManager.GameSpeedModifier));

		_gameManager.StartWaitingForPlayersRollCall -= OnStartWaitingForPlayersRollCall;
	}

	private void OnStartWaitingForPlayersRollCall()
	{
		HashSet<PlayerRef> charmedVillagers = _gameManager.GetPlayersFromPlayerGroup(PlayerGroups[1]);

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

		foreach (PlayerRef player in highlightedFor)
		{
			if (_networkDataManager.PlayerInfos[player].IsConnected)
			{
				_gameManager.RPC_SetPlayersCardHighlightVisible(player, charmedVillagers, false);
			}
		}
#if UNITY_SERVER && UNITY_EDITOR
		_gameManager.SetPlayersCardHighlightVisible(charmedVillagers, false);
#endif
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
			if (_gameManager.IsPlayerInPlayerGroup(playerInfo.Key, PlayerGroups[1]))
			{
				titlesOverride.Add(playerInfo.Key, _charmedVillagersImage.CompactTagId);
			}
			else
			{
				titlesOverride.Add(playerInfo.Key, _charmedVillagersRecognizingEachOtherImage.CompactTagId);
			}
		}
	}

	private void OnAddedPlayerToPlayerGroup(PlayerRef player, GameplayTag playerGroup)
	{
		if (Player == player && playerGroup == PlayerGroups[1])
		{
			_gameManager.SetPlayerGroupLeader(playerGroup, Player);
		}
		else if (_hasPower && Player == player && playerGroup == _werewolvesPlayerGroup)
		{
			_gameHistoryManager.AddEntry(_lostPowerGameHistoryEntry,
										new GameHistorySaveEntryVariable[] {
											new()
											{
												Name = "FlutePlayerPlayer",
												Data = _networkDataManager.PlayerInfos[Player].Nickname,
												Type = GameHistorySaveEntryVariableType.Player
											}
										});

			_hasPower = false;
			_gameManager.RemovePlayerFromPlayerGroup(Player, PlayerGroups[1]);

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

		_hasPower = !_gameManager.IsPlayerInPlayerGroup(Player, _werewolvesPlayerGroup);

		if (!hadPower && _hasPower)
		{
			_gameManager.AddedPlayerToPlayerGroup += OnAddedPlayerToPlayerGroup;
		}
		else if (!_hasPower)
		{
			_gameManager.RemovePlayerFromPlayerGroup(Player, PlayerGroups[1]);

			if (hadPower)
			{
				_gameManager.AddedPlayerToPlayerGroup -= OnAddedPlayerToPlayerGroup;
			}
		}

		if (_gameManager.IsPlayerInPlayerGroup(Player, PlayerGroups[1]))
		{
			_gameManager.SetPlayerGroupLeader(PlayerGroups[1], Player);
		}
	}

	public override void OnRoleCallDisconnected() { }

	private void OnDestroy()
	{
		_gameManager.AddedPlayerToPlayerGroup -= OnAddedPlayerToPlayerGroup;
	}
}
